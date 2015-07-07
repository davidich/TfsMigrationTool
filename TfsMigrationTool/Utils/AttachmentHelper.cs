using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMigrationTool.Utils
{
    public class AttachmentHelper
    {
        private static readonly Dictionary<int, string> FileCache = new Dictionary<int, string>();
        private static readonly string AttachmentsFolderPath = Config.AttachmentCacheFolder;

        static AttachmentHelper()
        {
            InitAttachmentCache();
        }

        private static void InitAttachmentCache()
        {
            if (!Directory.Exists(AttachmentsFolderPath))
            {
                Directory.CreateDirectory(AttachmentsFolderPath);
            }
            else
            {
                foreach (var attFolder in Directory.GetDirectories(AttachmentsFolderPath))
                {
                    var dirInfo = new DirectoryInfo(attFolder);
                    var attId = int.Parse(dirInfo.Name);

                    var filePath = Directory.GetFiles(attFolder).FirstOrDefault();

                    if (string.IsNullOrEmpty(filePath))
                    {
                        Directory.Delete(attFolder);
                    }
                    else
                    {
                        FileCache.Add(attId, filePath);
                    }
                }
            }
        }

        public static Attachment Copy(Attachment src)
        {
            if (FileCache.ContainsKey(src.Id) && File.Exists(FileCache[src.Id]))
            {
                var attachment = new Attachment(FileCache[src.Id]);
                return attachment;
            }

            using (var webClient = CreateWebClient())
            {
                var attFolderPath = Path.Combine(AttachmentsFolderPath, src.Id.ToString(CultureInfo.InvariantCulture));
                if (!Directory.Exists(attFolderPath))
                {
                    Directory.CreateDirectory(attFolderPath);
                }

                var filePath = Path.Combine(attFolderPath, src.Name);
                webClient.DownloadFile(src.Uri, filePath);

                FileCache.Add(src.Id, filePath);

                return new Attachment(filePath);
            }
        }

        private static WebClient CreateWebClient()
        {
            return new WebClient
                   {
                       Credentials = TfsAuthorizer.Authenticate()
                   };
        }
    }
}