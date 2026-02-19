using System.Net;
using System.Text;
using FAP.Domain.Models;

namespace FAP.Domain.Services
{
    /// <summary>
    /// Renders the browse page HTML from a strongly-typed BrowsePageData model.
    /// This is the compiled equivalent of Pages/Browse.cshtml.
    /// </summary>
    public static class BrowsePageRenderer
    {
        public static string Render(BrowsePageData model)
        {
            var sb = new StringBuilder(8192);

            sb.Append("""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <link rel="shortcut icon" type="image/ico" href="/Fap.app.web/favicon.ico" />
                    <title>
                """);
            sb.Append(Encode(model.Nickname));
            sb.Append("""
                's Web share</title>
                    <style type="text/css">
                        @import "/Fap.app.web/css/fap.css";
                    </style>
                    <script src="/Fap.app.web/js/jquery-1.5.2.min.js"></script>
                    <script src="/Fap.app.web/js/jquery.dataTables.min.js"></script>
                    <script>
                        $(document).ready(function () {
                            $('#files').dataTable({
                                "bPaginate": false,
                                "aaSorting": [],
                                "oLanguage": {
                                    "sZeroRecords": "No files or folders found!"
                                },
                                "aoColumns": [
                                    { "sWidth": "1px", "iDataSort": 6 },
                                    null,
                                    { "sWidth": "80px", "iDataSort": 4 },
                                    { "sWidth": "80px", "iDataSort": 5 },
                                    null,
                                    null,
                                    null
                                ]
                            });
                        })
                    </script>
                </head>
                <body id="body">
                    <div id="container">
                        <div class="full_width big">
                            <h1>
                """);
            sb.Append(Encode(model.Nickname));
            sb.Append("""
                's Shares</h1>
                        </div>
                        <p><span class="
                """);
            sb.Append(Encode(model.SlotColour));
            sb.Append("\">");
            sb.Append(model.FreeUploadSlots);
            sb.Append(" of ");
            sb.Append(model.MaxUploadSlots);
            sb.Append("""
                 upload slots available</span>.
                """);
            sb.Append(Encode(model.QueueInfo));
            sb.Append("  Files under ");
            sb.Append(Encode(model.FreeLimit));
            sb.Append("""
                 are not queued.</p>
                        <p><i>Please note that if the server runs out of upload slots then your download will stall until a free slot is available.</i></p>
                        <h2>Current Path:</h2>
                        <div>
                            <a href="/">Root</a>
                """);

            foreach (var segment in model.PathSegments)
            {
                sb.Append("<span class=\"PathSplitter\">/</span>");
                sb.Append("<a href=\"");
                sb.Append(Encode(segment.Path));
                sb.Append("\">");
                sb.Append(Encode(segment.Name));
                sb.Append("</a>");
            }

            sb.Append("""

                        </div>
                        <div id="demo">
                            <table cellpadding="0" cellspacing="0" border="0" class="display" id="files">
                                <thead>
                                    <tr>
                                        <th>Icon</th>
                                        <th>Name</th>
                                        <th>Modified</th>
                                        <th>Size</th>
                                    </tr>
                                </thead>
                                <tbody>
                """);

            foreach (var file in model.Files)
            {
                sb.Append("<tr class=\"even\"><td>");
                sb.Append(file.IconHtml); // Already contains HTML
                sb.Append("<a title=\"Download via FAP\" href=\"fap://");
                sb.Append(Encode(model.NodeId));
                sb.Append(Encode(model.CurrentPath));
                sb.Append(Encode(file.Path));
                sb.Append("\"><img alt=\"FAP Download\" src=\"/Fap.app.web/images/fap.png\" class=\"fapDownload\" /></a></td>");
                sb.Append("<td><a href=\"");
                sb.Append(Encode(model.CurrentPath));
                sb.Append(Encode(file.Path));
                sb.Append("\">");
                sb.Append(Encode(file.Name));
                sb.Append("</a></td>");
                sb.Append("<td>");
                sb.Append(Encode(file.LastModifiedText));
                sb.Append("</td>");
                sb.Append("<td class=\"center\">");
                sb.Append(Encode(file.SizeText));
                sb.Append("</td>");
                sb.Append("<td class=\"hidden\">");
                sb.Append(Encode(file.LastModified.ToString()));
                sb.Append("</td>");
                sb.Append("<td class=\"hidden\">");
                sb.Append(file.Size);
                sb.Append("</td>");
                sb.Append("<td class=\"hidden\">");
                sb.Append(Encode(file.Icon));
                sb.Append("</td></tr>\n");
            }

            sb.Append("""

                                </tbody>
                                <tfoot>
                                    <tr>
                                        <th>Icon</th>
                                        <th>Name</th>
                                        <th>Modified</th>
                                        <th>Size</th>
                                    </tr>
                                </tfoot>
                            </table>
                            <div style="text-align:center">
                                Total Size: 
                """);
            sb.Append(Encode(model.TotalSize));
            sb.Append("""

                            </div>
                            <div class="spacer"></div>
                            <div style="text-align:center">
                                Page generated by 
                """);
            sb.Append(Encode(model.AppVersion));
            sb.Append("""

                            </div>
                        </div>
                    </div>
                </body>
                </html>
                """);

            return sb.ToString();
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
