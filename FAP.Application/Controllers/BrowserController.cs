#region Copyright Kayomani 2011.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.

/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */

#endregion

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Controls;
using Fap.Foundation.Threading;
using FAP.Application.ViewModels;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Entities.FileSystem;
using System.Net.Http;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Fap.Foundation;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class BrowserController
    {
        private readonly BrowserViewModel bvm;
        private readonly Node client;
        private readonly Model model;
        private readonly ShareInfoService shareInfo;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ModernHttpClient> _httpLogger;

        public BrowserController(BrowserViewModel bvm, Model model, Node client, ShareInfoService i,
            IHttpClientFactory httpClientFactory, ILogger<ModernHttpClient> httpLogger)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client), "Client node cannot be null");
            this.model = model ?? throw new ArgumentNullException(nameof(model), "Model cannot be null");
            this.bvm = bvm ?? throw new ArgumentNullException(nameof(bvm), "BrowserViewModel cannot be null");
            shareInfo = i ?? throw new ArgumentNullException(nameof(i), "ShareInfoService cannot be null");
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _httpLogger = httpLogger ?? throw new ArgumentNullException(nameof(httpLogger));
            bvm.NoCache = model.AlwaysNoCacheBrowsing;
        }

        public BrowserViewModel ViewModel
        {
            get { return bvm; }
        }

        public void Initalise()
        {
            bvm.Download = new RelayCommand(Download);
            bvm.Refresh = new RelayCommand(Refresh);
            bvm.PropertyChanged += bvm_PropertyChanged;
            //Pull down the inital listing
            bvm.Status = "Getting initial share list..";
            Populate("");
        }

        private void bvm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentPath")
            {
                Populate(bvm.CurrentPath);
            }
        }

        private void Populate(string ent)
        {
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogDebug("Populate: Starting with path='{Path}'", ent);

            // Normalize to forward slashes for BrowsingFile.FullPath / BrowseVerb paths.
            ent = (ent ?? string.Empty).Replace('\\', '/').Trim('/');
            bvm.IsBusy = true;
            if (string.IsNullOrEmpty(ent))
            {
                logger.LogDebug("Populate: Empty path, clearing root and starting root browse");
                bvm.Root.Clear();
                _ = Task.Run(() => PopulateAsync(null));
                return;
            }
            string[] items = ent.Split('/', StringSplitOptions.RemoveEmptyEntries);
            logger.LogDebug("Populate: Split path into {Count} items: [{Items}]", items.Length, string.Join(", ", items));
            BrowsingFile? parent = bvm.Root.Where(n => n.Name == items[0]).FirstOrDefault();
            logger.LogDebug("Populate: Found parent in Root: {Parent}", parent?.Name ?? "null");

            if (parent == null)
            {
                // If we can't find the parent in Root, this might be a direct share name
                // Create a temporary BrowsingFile to represent this share
                logger.LogDebug("Populate: Parent not found in Root, creating temporary share for '{Item}'", items[0]);
                var tempShare = new BrowsingFile();
                tempShare.FullPath = ent;
                tempShare.IsFolder = true;
                _ = Task.Run(() => PopulateAsync(tempShare));
                return;
            }

            if (items.Length == 1)
            {
                // This is a root share being expanded
                logger.LogDebug("Populate: Single item path, expanding root share '{Name}'", parent.Name);
                if (!parent.IsPopulated || bvm.NoCache)
                {
                    logger.LogDebug("Populate: Share not populated or no cache, starting populate for '{Name}'", parent.Name);
                    parent.ClearItems();
                    _ = Task.Run(() => PopulateAsync(parent));
                }
                else
                {
                    logger.LogDebug("Populate: Share already populated, setting as current item");
                    bvm.CurrentItem = parent;
                    bvm.IsBusy = false;
                }
                return;
            }

            // Navigate through subdirectories
            for (int i = 1; i < items.Length; i++)
            {
                BrowsingFile? search = parent.Items.Where(n => n.Name == items[i]).FirstOrDefault();
                if (null == search)
                {
                    var fse = new BrowsingFile
                    {
                        IsFolder = true,
                        FullPath = string.Join('/', items.Take(i + 1))
                    };
                    parent.Items.Add(fse);
                    parent = fse;
                }
                else
                {
                    parent = search;
                }
            }

            if (!parent.IsPopulated || bvm.NoCache)
            {
                parent.ClearItems();
                _ = Task.Run(() => PopulateAsync(parent));
            }
            else
            {
                bvm.CurrentItem = parent;
                bvm.IsBusy = false;
            }
        }


        private async void PopulateAsync(object? o)
        {
            try
            {
                // Add verbose logging to understand what's happening
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                logger.LogDebug("PopulateAsync: Starting with client={Client}, model={Model}", client?.Nickname ?? "null", model?.Nickname ?? "null");
                
                // Check if required objects are available
                if (client == null)
                {
                    logger.LogWarning("PopulateAsync: Client is null");
                    SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                               delegate
                                                                   {
                                                                       bvm.Status = "Error: No client available for browsing.";
                                                                       bvm.IsBusy = false;
                                                                   }
                                                           ));
                    return;
                }

                if (model?.LocalNode == null)
                {
                    logger.LogWarning("PopulateAsync: Model or LocalNode is null");
                    SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                               delegate
                                                                   {
                                                                       bvm.Status = "Error: Local node not available.";
                                                                       bvm.IsBusy = false;
                                                                   }
                                                           ));
                    return;
                }

                logger.LogDebug("PopulateAsync: Client={Nickname}, Host={Host}, ID={Id}", client.Nickname, client.Host, client.ID);
                logger.LogDebug("PopulateAsync: Model={Nickname}, LocalNode={LocalNickname}", model.Nickname, model.LocalNode.Nickname);

                var fse = o as BrowsingFile;
                if (null != fse)
                {
                    try
                    {
                        logger.LogDebug("PopulateAsync: Creating ModernHttpClient for path={Path}", fse.FullPath);
                        var c = new ModernHttpClient(model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                        var cmd = new BrowseVerb(shareInfo);
                        cmd.Path = fse.FullPath;
                        cmd.NoCache = bvm.NoCache;
                        
                        logger.LogDebug("PopulateAsync: About to execute command with client={Nickname}", client.Nickname);
                        var result = await c.ExecuteAsync(cmd, client);
                        logger.LogDebug("PopulateAsync: Execute result = {Result}", result);
                         if (result)
                         {
                             logger.LogDebug("PopulateAsync: Command executed successfully, Results count = {Count}", cmd.Results?.Count ?? 0);
                             try
                             {
                                 SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                            delegate
                                                                                {
                                                                                    try
                                                                                    {
                                                                                        if (cmd.Results != null)
                                                                                        {
                                                                                             logger.LogDebug("PopulateAsync: Processing {Count} results", cmd.Results.Count);
                                                                                            bvm.Status = "Download complete (" +
                                                                                                         cmd.Results.Count + ").";
                                                                                            fse.IsPopulated = true;
                                                                                            fse.ClearItems();

                                                                                            foreach (BrowsingFile browseResult in cmd.Results)
                                                                                            {
                                                                                                browseResult.Path = fse.FullPath;
                                                                                                fse.AddItem(browseResult);
                                                                                            }
                                                                                            bvm.CurrentItem = fse;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            logger.LogDebug("PopulateAsync: No results returned from browse operation");
                                                                                            bvm.Status = "No results returned from browse operation.";
                                                                                        }
                                                                                        bvm.IsBusy = false;
                                                                                    }
                                                                                    catch (Exception ex)
                                                                                    {
                                                                                         logger.LogError(ex, "PopulateAsync: Error updating UI after successful browse");
                                                                                        bvm.Status = $"Error updating UI: {ex.Message}";
                                                                                        bvm.IsBusy = false;
                                                                                    }
                                                                                }
                                                                            ));
                             }
                             catch (Exception ex)
                             {
                                  logger.LogError(ex, "PopulateAsync: Error invoking dispatcher for UI update");
                                 bvm.Status = $"Error updating UI: {ex.Message}";
                                 bvm.IsBusy = false;
                             }
                         }
                        else
                        {
                            if (SafeObservableStatic.UiDispatcher != null)
                            {
                                SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                           delegate
                                                                               {
                                                                                   if (bvm != null)
                                                                                   {
                                                                                       bvm.Status = "Failed to execute browse command.";
                                                                                       bvm.IsBusy = false;
                                                                                   }
                                                                               }
                                                                           ));
                            }
                            else
                            {
                            logger.LogWarning("PopulateAsync: SafeObservableStatic.UiDispatcher is null, cannot update UI for failed browse command");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (SafeObservableStatic.UiDispatcher != null)
                        {
                            SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                       delegate
                                                                           {
                                                                               if (bvm != null)
                                                                               {
                                                                                   bvm.Status = $"Error executing browse command: {ex.Message}";
                                                                                   bvm.IsBusy = false;
                                                                               }
                                                                           }
                                                                       ));
                        }
                        else
                        {
                            logger.LogWarning("PopulateAsync: SafeObservableStatic.UiDispatcher is null, cannot update UI for browse command error");
                        }
                    }
                }
                else
                {
                    try
                    {
                        logger.LogDebug("PopulateAsync: Creating ModernHttpClient for root browse");
                        var c = new ModernHttpClient(model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                        var cmd = new BrowseVerb(shareInfo);
                        cmd.Path = ""; // Root path for initial browse
                        cmd.NoCache = bvm.NoCache;

                        logger.LogDebug("PopulateAsync: About to execute root command with client={Nickname}", client.Nickname);
                        var result = await c.ExecuteAsync(cmd, client);
                        logger.LogDebug("PopulateAsync: Execute result = {Result}", result);
                         if (result)
                         {
                             logger.LogDebug("PopulateAsync: Root command executed successfully, Results count = {Count}", cmd.Results?.Count ?? 0);
                             try
                             {
                                 if (SafeObservableStatic.UiDispatcher == null)
                                 {
                                    logger.LogWarning("PopulateAsync: SafeObservableStatic.UiDispatcher is null, cannot update UI");
                                     return;
                                 }
                                 
                                 SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                            delegate
                                                                                {
                                                                                    try
                                                                                    {
                                                                                        if (bvm == null)
                                                                                        {
                                                                                            logger.LogWarning("PopulateAsync: BrowserViewModel is null, skipping UI update");
                                                                                            return;
                                                                                        }
                                                                                        
                                                                                        if (cmd.Results != null)
                                                                                        {
                                                                                            logger.LogDebug("PopulateAsync: Processing {Count} root results", cmd.Results.Count);
                                                                                            bvm.Status = "Download complete (" +
                                                                                                         cmd.Results.Count + ").";
                                                                                            var ent = new BrowsingFile();
                                                                                            foreach (BrowsingFile browseResult in cmd.Results)
                                                                                            {
                                                                                                // Ensure FullPath is set correctly for root shares
                                                                                                if (string.IsNullOrEmpty(browseResult.Path))
                                                                                                {
                                                                                                    browseResult.FullPath = browseResult.Name;
                                                                                                }
                                                                                                logger.LogDebug("PopulateAsync: Adding root share '{Name}' with FullPath='{FullPath}'", browseResult.Name, browseResult.FullPath);
                                                                                                bvm.Root.Add(browseResult);
                                                                                                ent.AddItem(browseResult);
                                                                                            }
                                                                                            ent.IsPopulated = true;
                                                                                            bvm.CurrentItem = ent;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            logger.LogDebug("PopulateAsync: No results returned from root browse operation");
                                                                                            bvm.Status = "No results returned from browse operation.";
                                                                                        }
                                                                                        bvm.IsBusy = false;
                                                                                    }
                                                                                    catch (Exception ex)
                                                                                    {
                        logger.LogError(ex, "PopulateAsync: Error updating UI after successful root browse");
                                                                                        if (bvm != null)
                                                                                        {
                                                                                            bvm.Status = $"Error updating UI: {ex.Message}";
                                                                                            bvm.IsBusy = false;
                                                                                        }
                                                                                    }
                                                                                }
                                                                            ));
                             }
                             catch (Exception ex)
                             {
                 logger.LogError(ex, "PopulateAsync: Error invoking dispatcher for root browse UI update");
                                 if (bvm != null && SafeObservableStatic.UiDispatcher != null)
                                 {
                                     try
                                     {
                                         SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                                    delegate
                                                                                        {
                                                                                            bvm.Status = $"Error updating UI: {ex.Message}";
                                                                                            bvm.IsBusy = false;
                                                                                        }
                                                                                    ));
                                     }
                                      catch (Exception dispatcherEx)
                                      {
                                          logger.LogError(dispatcherEx, "PopulateAsync: Error updating status after dispatcher error");
                                      }
                                 }
                             }
                         }
                        else
                        {
                            SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                       delegate
                                                                           {
                                                                               bvm.Status = "Failed to execute browse command.";
                                                                               bvm.IsBusy = false;
                                                                           }
                                                                       ));
                        }
                    }
                    catch (Exception ex)
                    {
                        if (SafeObservableStatic.UiDispatcher != null)
                        {
                            SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                                       delegate
                                                                           {
                                                                               if (bvm != null)
                                                                               {
                                                                                   bvm.Status = $"Error executing browse command: {ex.Message}";
                                                                                   bvm.IsBusy = false;
                                                                               }
                                                                           }
                                                                       ));
                        }
                        else
                        {
                    logger.LogWarning("PopulateAsync: SafeObservableStatic.UiDispatcher is null, cannot update UI for browse command error");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (SafeObservableStatic.UiDispatcher != null)
                {
                    SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                                                               delegate
                                                                   {
                                                                       if (bvm != null)
                                                                       {
                                                                           bvm.Status = $"Error during browse operation: {ex.Message}";
                                                                           bvm.IsBusy = false;
                                                                       }
                                                                   }
                                                               ));
                }
                else
                {
                    var logger2 = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                    logger2.LogWarning("PopulateAsync: SafeObservableStatic.UiDispatcher is null, cannot update UI for browse operation error");
                }
            }
        }

        private void Download()
        {
            for (int i = 0; i < bvm.LastSelectedEntity.Count; i++)
            {
                BrowsingFile ent = bvm.LastSelectedEntity[i];

                model.DownloadQueue.List.Add(new DownloadRequest
                                                 {
                                                     Added = DateTime.Now,
                                                     FullPath = ent.FullPath,
                                                     IsFolder = ent.IsFolder,
                                                     Size = ent.Size,
                                                     State = DownloadRequestState.None,
                                                     ClientID = client.ID,
                                                     Nickname = client.Nickname
                                                 });

                if (bvm.LastSelectedEntity.Count == 1)
                    bvm.Status = "Queued download of: " + ent.FullPath;
                else
                    bvm.Status = "Queued " + bvm.LastSelectedEntity.Count + " downloads.";
            }
        }


        private void Refresh()
        {
            if (null != bvm.LastSelectedEntity)
            {
                bvm.Status = "Refreshing current file list.. ";
                _ = Task.Run(() => item_selected_async(bvm.LastSelectedEntity));
            }
        }


        private void item_Selected(object? sender, RoutedEventArgs e)
        {
            var src = e.Source as TreeViewItem;

            if (null != src)
            {
                if (!src.IsExpanded)
                {
                    src.IsExpanded = true;
                }
                else
                {
                    var path = src.Tag as BrowsingFile;
                    if (null != path)
                        bvm.Status = "Downloading: " + path.FullPath;
                    _ = Task.Run(() => item_selected_async(path));
                }
                e.Handled = true;
            }
        }

        private void item_selected_async(object? input)
        {
            var c = new Client(model.LocalNode);
            var cmd = new BrowseVerb(shareInfo);
            cmd.NoCache = bvm.NoCache;
            var ent = input as BrowsingFile;
            if (null != ent)
                cmd.Path = ent.FullPath;

            /*  bvm.CurrentDirectory.Clear();
              if (c.Execute(cmd, client))
              {
                  SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                     delegate()
                     {
                         bvm.Status = "Download complete (" + cmd.Results.Count + ").";
                         foreach (var result in cmd.Results)
                         {
                             bvm.CurrentDirectory.Add(result);
                         }
                     }
                    ));
              }*/
        }

        private void item_Expanded(object? sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender!;
            item.Items.Clear();
            var path = item.Tag as BrowsingFile;
            if (null != path)
                bvm.Status = "Downloading: " + path.FullPath;
            _ = Task.Run(() => item_Expanded_Async(new ExpandRequest {Item = item, Path = path!}));
            e.Handled = true;
        }

        private void item_Expanded_Async(object? input)
        {
            var req = input as ExpandRequest;
            if (req == null) return;
            var c = new Client(model.LocalNode);
            var cmd = new BrowseVerb(shareInfo);
            cmd.Path = req.Path.FullPath;
            cmd.NoCache = bvm.NoCache;
            if (c.Execute(cmd, client))
            {
                /*  SafeObservableStatic.UiDispatcher?.Invoke(new Action(
                  delegate()
                  {
                      bvm.Status = "Download complete (" + cmd.Results.Count + " items).";
                    bvm.CurrentDirectory.Clear();
                      foreach (var result in cmd.Results)
                      {
                          if (result.IsFolder)
                          {
                              TreeViewItem x = new TreeViewItem();
                              x.Items.Add(_dummyNode);
                              x.Expanded += new System.Windows.RoutedEventHandler(item_Expanded);
                              x.Selected += new System.Windows.RoutedEventHandler(item_Selected);
                              x.Header = result.Name;
                              x.Tag = result;
                              req.Item.Items.Add(x);
                          }
                          bvm.CurrentDirectory.Add(result);
                      }
                  }
                 ));*/
            }
        }

        #region Nested type: ExpandRequest

        private class ExpandRequest
        {
            public BrowsingFile Path { set; get; } = null!;
            public TreeViewItem Item { set; get; } = null!;
        }

        #endregion
    }
}