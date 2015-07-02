namespace TfsMigrationTool
{
    using System.Net;

    using Microsoft.TeamFoundation.Client;

    public static class ServiceFactory
    {
        private static readonly NetworkCredential Credentials;

        static ServiceFactory()
        {
            Credentials = TfsAuthorizer.Authenticate();
        }

        public static T Create<T>()
        {
            var projectCollection = new TfsTeamProjectCollection(Config.TfsCollectionUrl, Credentials);
            return projectCollection.GetService<T>();               
        }        
    }
}