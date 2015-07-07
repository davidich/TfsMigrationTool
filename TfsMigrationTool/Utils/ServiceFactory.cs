using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.TeamFoundation.Client;

namespace TfsMigrationTool.Utils
{
    public static class ServiceFactory
    {
        private static readonly Dictionary<Type, object> ServiceCache = new Dictionary<Type, object>();
        private static readonly NetworkCredential Credentials;

        static ServiceFactory()
        {
            Credentials = TfsAuthorizer.Authenticate();
        }

        public static T Create<T>()
        {
            object service;

            if (!ServiceCache.TryGetValue(typeof (T), out service))
            {
                var projectCollection = new TfsTeamProjectCollection(Config.TfsCollectionUrl, Credentials);
                service = projectCollection.GetService<T>();
                ServiceCache.Add(typeof (T), service);
                
            }

            return (T) service;
        }        
    }
}