namespace TfsMigrationTool
{
    using System;

    public static class Config
    {
        public static readonly Uri TfsCollectionUrl = new Uri("http://tfs2012.deloitte.com:8080/tfs/ITS");
        public static readonly string AttachmentCacheFolder = "C:\\Temp\\";        
    }
}