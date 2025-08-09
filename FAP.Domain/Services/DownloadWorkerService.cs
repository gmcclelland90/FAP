using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading;
using FAP.Domain.Entities;
using FAP.Domain.Entities.FileSystem;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using Fap.Foundation;
using FAP.Network;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace FAP.Domain.Services
{
    /// <summary>
    /// Handles processing a download queue 
    /// </summary>
    public class DownloadWorkerService : ITransferWorker
    {
        private readonly BufferService bufferService;
        private readonly Model model;
        private readonly NetworkSpeedMeasurement netSpeed = new NetworkSpeedMeasurement(NetSpeedType.Download);
        private readonly Queue<DownloadRequest> queue = new Queue<DownloadRequest>();
        private readonly Node remoteNode;
        private readonly object sync = new object();
        private readonly HttpClient httpClient;
        private bool isComplete = true;
        private long length;
        private long position;
        private string status = string.Empty;

        private readonly ILogger<DownloadWorkerService> logger;

        public DownloadWorkerService(Node n, Model m, BufferService b, ILogger<DownloadWorkerService> logger)
        {
            remoteNode = n;
            model = m;
            bufferService = b;
            this.logger = logger;
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Model.AppVersion);
        }

        public bool IsQueueFull
        {
            get
            {
                lock (sync)
                {
                    if (queue.Count > 10)
                        return true;
                    if (queue.Sum(s => s.Size) > 262144000) // 256mb
                        return true;
                    return false;
                }
            }
        }

        public Node Node
        {
            get { return remoteNode; }
        }

        #region ITransferWorker Members

        public long Length
        {
            get { return length; }
        }

        public bool IsComplete
        {
            get { return isComplete; }
        }

        public long Speed
        {
            get { return netSpeed.GetSpeed(); }
        }

        public string Status
        {
            get { return status; }
        }

        public long Position
        {
            get { return position; }
        }

        #endregion

        public event EventHandler OnWorkerFinished = null!;

        public void AddDownload(DownloadRequest item)
        {
            lock (sync)
            {
                queue.Enqueue(item);
                if (isComplete)
                {
                    isComplete = false;
                    _ = Task.Run(processAsync);
                }
                item.State = DownloadRequestState.Queued;
            }
        }

        private async Task processAsync()
        {
            try
            {
                while (true)
                {
                    DownloadRequest currentItem = null;

                    bool QueueEmpty = false;

                    lock (sync)
                        QueueEmpty = queue.Count == 0;
                    if (QueueEmpty)
                        OnWorkerFinished?.Invoke(this, EventArgs.Empty);

                    lock (sync)
                    {
                        if (queue.Count > 0)
                            currentItem = queue.Dequeue();
                        if (currentItem == null)
                        {
                            isComplete = true;
                            return;
                        }
                    }

                    currentItem.State = DownloadRequestState.Downloading;

                    if (currentItem.IsFolder)
                    {
                        length = 0;
                        position = 0;
                        status = "Downloading folder info for " + currentItem.FullPath;
                        //Item is a folder - Just get the folder items and add them to the queue.
                        var verb = new BrowseVerb(null);
                        verb.Path = currentItem.FullPath;
                        //Always get the latest info.
                        verb.NoCache = true;

                        var client = new Client(null!);

                        if (client.Execute(verb, remoteNode))
                        {
                            currentItem.State = DownloadRequestState.Downloaded;
                            var newItems = new List<DownloadRequest>();

                            foreach (BrowsingFile item in verb.Results)
                            {
                                newItems.Add(new DownloadRequest
                                                 {
                                                     Added = DateTime.Now,
                                                     ClientID = remoteNode.ID,
                                                     FullPath = currentItem.FullPath + "/" + item.Name,
                                                     IsFolder = item.IsFolder,
                                                     LocalPath = currentItem.LocalPath + "\\" + currentItem.FileName,
                                                     NextTryTime = 0,
                                                     Nickname = remoteNode.Nickname,
                                                     Size = item.Size,
                                                     State = DownloadRequestState.None
                                                 });
                            }
                            model.DownloadQueue.List.AddRange(newItems);
                        }
                        else
                        {
                            currentItem.State = DownloadRequestState.Error;
                            currentItem.NextTryTime = Environment.TickCount + Model.DOWNLOAD_RETRY_TIME;
                        }
                    }
                    else
                    {
                        MemoryBuffer buffer = bufferService.GetBuffer();
                        buffer.SetDataLocation(0, buffer.Data.Length);
                        //Item is a file - download it
                        try
                        {
                            length = currentItem.Size;
                            position = 0;
                            status = $"{currentItem.Nickname} - {currentItem.FileName} - Connecting..";
                            currentItem.State = DownloadRequestState.Downloading;

                            string mainPath = string.Empty;
                            string mainFolder = string.Empty;
                            string incompletePath = string.Empty;
                            ;
                            string incompleteFolder = string.Empty;

                            //Build paths
                            var mainsb = new StringBuilder();
                            mainsb.Append(model.DownloadFolder);
                            if (!string.IsNullOrEmpty(currentItem.LocalPath))
                            {
                                mainsb.Append("\\");
                                mainsb.Append(currentItem.LocalPath);
                            }
                            mainFolder = mainsb.ToString();
                            mainsb.Append("\\");
                            mainsb.Append(currentItem.FileName);
                            mainPath = mainsb.ToString();

                            var incompletesb = new StringBuilder();
                            incompletesb.Append(model.IncompleteFolder);
                            if (!string.IsNullOrEmpty(currentItem.LocalPath))
                            {
                                incompletesb.Append("\\");
                                incompletesb.Append(currentItem.LocalPath);
                            }
                            incompleteFolder = incompletesb.ToString();
                            incompletesb.Append("\\");
                            incompletesb.Append(currentItem.FileName);
                            incompletePath = incompletesb.ToString();


                            FileStream fileStream = null!;

                            //Check to see if the file already exists.
                            if (File.Exists(mainPath))
                            {
                                //File exists in the download directory.
                                fileStream = File.Open(mainPath, FileMode.Open, FileAccess.Write, FileShare.None);
                                incompletePath = mainPath;
                            }
                            else
                            {
                                if (!Directory.Exists(incompleteFolder))
                                    Directory.CreateDirectory(incompleteFolder);

                                //Else resume or just create
                                fileStream = File.Open(incompletePath, FileMode.OpenOrCreate, FileAccess.Write,
                                                       FileShare.None);
                            }

                            var req = new HttpRequestMessage(HttpMethod.Get, Multiplexor.Encode(getDownloadUrl(), "GET", currentItem.FullPath));
                            req.Headers.Add("FAP-SOURCE", model.LocalNode.ID);

                            //If we are resuming then add range
                            long resumePoint = 0;
                            if (fileStream.Length != 0)
                            {
                                //Yes Microsoft if you read this... OH WHY IS ADDRANGE ONLY AN INT?? We live in an age where we might actually download more than 2gb
                                //req.AddRange(fileStream.Length);

                                //Hack
                                string val = string.Format("bytes={0}-", fileStream.Length);
                                req.Headers.Add("Range", val);
                                position = fileStream.Length;
                                resumePoint = fileStream.Length;
                                //Seek to the end of the file
                                fileStream.Seek(fileStream.Length, SeekOrigin.Begin);
                            }

                            var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                            if (resp.IsSuccessStatusCode)
                            {
                                using (Stream responseStream = await resp.Content.ReadAsStreamAsync())
                                {
                                    var tokenizer = new StreamTokenizer(Encoding.ASCII, "|");
                                    var utilisedBuffers = new List<MemoryBuffer>();
                                    try
                                    {
                                        bool streamIncomplete = true;

                                        while (streamIncomplete)
                                        {
                                            MemoryBuffer tokenBuffer = bufferService.GetSmallBuffer();
                                            //utilisedBuffers.Add(tokenBuffer);
                                            //Receive data
                                            tokenBuffer.SetDataLocation(0,
                                                                        responseStream.Read(tokenBuffer.Data, 0,
                                                                                            tokenBuffer.DataSize));
                                            tokenizer.ReceiveData(tokenBuffer);

                                            if (tokenizer.ContainsCommand())
                                            {
                                                string data = tokenizer.GetCommand();
                                                int queuePosition = int.Parse(data);

                                                if (queuePosition == 0)
                                                {
                                                    if (tokenizer.Buffers.Count > 0)
                                                    {
                                                        // TODO: Replace with injected logger when refactoring constructor
                                                        // logger.LogWarning("Queue info overlaps with file data.  File: {File}", currentItem.FileName);
                                                        //Due to the way chunks are delivered we should never get here
                                                        //Just incase write left over data
                                                        foreach (MemoryBuffer buff in tokenizer.Buffers)
                                                            fileStream.Write(buff.Data, 0, buff.DataSize);
                                                    }

                            status = $"{currentItem.Nickname} - {currentItem.FileName} - {Utility.FormatBytes(currentItem.Size)}";

                                                    var sw = System.Diagnostics.Stopwatch.StartNew();

                                                    while (true)
                                                    {
                                                        //Receive file
                                                        int read = responseStream.Read(buffer.Data, 0,
                                                                                       buffer.Data.Length);
                                                        if (read == 0)
                                                        {
                                                            streamIncomplete = false;
                                                            break;
                                                        }
                                                        else
                                                        {
                                                            fileStream.Write(buffer.Data, 0, read);
                                                            position += read;
                                                            netSpeed.PutData(read);
                                                        }
                                                    }

                                                    //Add log of transfer
                                                    sw.Stop();
                                                    double seconds = sw.Elapsed.TotalSeconds;
                                                    var rxlog = new TransferLog();
                                                    rxlog.Added = currentItem.Added;
                                                    rxlog.Completed = DateTime.Now;
                                                    rxlog.Filename = currentItem.FileName;
                                                    rxlog.Nickname = currentItem.Nickname;
                                                    rxlog.Path = currentItem.FolderPath;
                                                    rxlog.Size = currentItem.Size - resumePoint;
                                                    if (seconds > 0)
                                                        rxlog.Speed = (int)(rxlog.Size / seconds);
                                                    model.CompletedDownloads.Add(rxlog);
                                                    logger.LogInformation("Download completed: {File} bytes={Bytes} durationMs={DurationMs} avgKbps={Kbps}",
                                                        currentItem.FileName, rxlog.Size, sw.ElapsedMilliseconds,
                                                        seconds > 0 ? (rxlog.Size / 1024.0) / seconds : 0);
                                                }
                                                else
                                                {
                                                    //Queued
                                                    status = $"{currentItem.Nickname} - {currentItem.FileName} - Queue position {queuePosition}";
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        foreach (MemoryBuffer buff in utilisedBuffers)
                                            bufferService.FreeBuffer(buff);
                                        tokenizer.Dispose();
                                    }
                                }
                            }

                            resp.Dispose();
                            model.DownloadQueue.List.Remove(currentItem);
                            currentItem.State = DownloadRequestState.Downloaded;
                            fileStream.Close();
                            fileStream.Dispose();
                            //Move from the incomplete folder.
                            if (mainPath != incompletePath)
                            {
                                if (!Directory.Exists(mainFolder))
                                    Directory.CreateDirectory(mainFolder);
                                File.Move(incompletePath, mainPath);
                            }
                            status = $"{currentItem.Nickname} - Complete: {currentItem.FileName}";
                            resp.Dispose();
                        }
                        catch (Exception ex)
                        {
                            currentItem.State = DownloadRequestState.Error;
                            currentItem.NextTryTime = Environment.TickCount + Model.DOWNLOAD_RETRY_TIME;
                            logger.LogError(ex, "DownloadWorkerService.processAsync: Error downloading {File}", currentItem.FileName);
                        }
                        finally
                        {
                            bufferService.FreeBuffer(buffer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Something went very wrong.  Clear the queue and die.
                logger.LogError(ex, "DownloadWorkerService.processAsync: Fatal error, clearing queue");
                lock (sync)
                {
                    isComplete = true;
                    foreach (DownloadRequest v in queue)
                        v.State = DownloadRequestState.None;
                    queue.Clear();
                }
            }
        }


        private string getDownloadUrl()
        {
            var sb = new StringBuilder();
            sb.Append("http://");
            sb.Append(remoteNode.Location);
            if (!remoteNode.Location.EndsWith("/"))
                sb.Append("/");
            return sb.ToString();
        }
    }
}