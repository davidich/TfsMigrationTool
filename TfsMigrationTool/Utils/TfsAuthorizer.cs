using System;
using System.IO;
using System.Net;
using Microsoft.TeamFoundation.Client;

namespace TfsMigrationTool.Utils
{
    public class TfsAuthorizer
    {
        private static NetworkCredential credentials;
        private static readonly string CridentialCachePath = Path.Combine(Environment.CurrentDirectory, "cred.cache");

        private TfsAuthorizer()
        {

        }

        public static NetworkCredential Authenticate()
        {
            if (credentials == null)
            {
            Start:
                NetworkCredential credential;

                if (!TryReadCredentials(out credential))
                {
                    var login = AskLogin();
                    var password = AskPassword();
                    credential = new NetworkCredential(login, password);
                }

                try
                {
                    Console.WriteLine("-----Authentication------");
                    Console.WriteLine("Started...");

                    var projectCollection = new TfsTeamProjectCollection(Config.TfsCollectionUrl, credential);
                    projectCollection.Authenticate();

                    credentials = credential;

                    WriteCredentials(credential.UserName, credential.Password);

                    Console.WriteLine("Successfully completed.");
                    Console.WriteLine("-------------------------");
                    Console.WriteLine("");
                }
                catch (Microsoft.TeamFoundation.TeamFoundationServerUnauthorizedException ex)
                {
                    ClearCredentials();

                    Console.Clear();
                    Console.WriteLine("Couldn't connect to TFS. Try again");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("");
                    goto Start;
                }
            }

            return credentials;
        }

        private static void WriteCredentials(string login, string pass)
        {
            using (var writer = File.CreateText(CridentialCachePath))
            {
                writer.WriteLine(login);
                writer.WriteLine(pass);
            }
        }

        private static bool TryReadCredentials(out NetworkCredential cred)
        {
            cred = new NetworkCredential();

            if (!File.Exists(CridentialCachePath))
                return false;

            using (var reader = File.OpenText(CridentialCachePath))
            {
                cred.UserName = reader.ReadLine();
                cred.Password = reader.ReadLine();

                return true;
            }
        }

        private static void ClearCredentials()
        {
            if (File.Exists(CridentialCachePath))
            {
                File.Delete(CridentialCachePath);
            }
        }

        private static string AskLogin()
        {
            var login = string.Empty;
            while (string.IsNullOrWhiteSpace(login))
            {
                Console.WriteLine("Enter you login:");
                login = Console.ReadLine();
            }

            if (!login.StartsWith("us\\"))
            {
                login = "us\\" + login;
            }

            return login;
        }

        private static string AskPassword()
        {
            string password = string.Empty;

            while (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Enter you password:");

                ConsoleKeyInfo key;
                do
                {
                    key = Console.ReadKey(true);

                    if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                    {
                        password += key.KeyChar;
                        Console.Write("*");
                    }
                    else
                    {
                        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                        {
                            password = password.Substring(0, (password.Length - 1));
                            Console.Write("\b \b");
                        }

                    }
                } while (key.Key != ConsoleKey.Enter);
            }

            return password;
        }
    }
}