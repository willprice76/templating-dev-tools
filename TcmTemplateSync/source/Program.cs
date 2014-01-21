using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tridion.Extensions.Deployment.TemplateUpload
{
    class Program
    {
        private const int ERROR_BAD_ARGUMENTS = 0xA0;
        private const int ERROR_INVALID_COMMAND_LINE = 0x667;

        /// <summary>
        /// Uploads TBBs from local file system to CMS, and/or downloads 
        /// the latest TBBs from the CMS to the local FS.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            bool show_help = false;
            Config config = new Config();
            string action = null;
            var p = new OptionSet() {
                { "folder:", "The TCMURI of the Tridion folder to sync with",
                  v => config.RootFolderUri = v },
                { "action:", "The sync action to take: u=upload to CMS only (default behaviour), d=download from CMS only, ud=upload and then download.",
                  v => action = v },
                { "extensions:", "Commma delimited set of TBB extensions to process (default is cshtml only, but xslt, dwt and tbbcs are also valid)",
                  v => config.TbbExtensions = v.Split(',').Select(s=>s.Trim()).ToList()},
                { "endpoint:", "The Core service endpoint to use (for example net.tcp://my-cms-server:2660/CoreService/2011/netTcp (2011 SP1 Net Tcp) or http://my-cms-server/webservices/CoreService2013.svc/wsHttp (2013 SP1 Ws Http)",
                  v => config.TargetUrl = v },
                { "username:", "Username to be used in authentication (if missing will use user running the process)",
                  v => config.Username = v },
                { "password:", "Password to be used in authentication (if missing will use user running the process)",
                  v => config.Password = v },
                { "help",  "print this message", 
                  v => show_help = v != null },
            };
            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try 'TcmTemplateSync /help' for more information.");
                return;
            }
            if (show_help)
            {
                ShowHelp(p);
                return;
            }
            if (extra.Count == 0)
            {
                Console.Write("ERROR: You must specify a root directory to process");
                ShowHelp(p);
                Environment.ExitCode = ERROR_INVALID_COMMAND_LINE;
                return;
            }
            else
            {
                config.LocalFolderRoot = extra[0];
                {
                    if (!Directory.Exists(config.LocalFolderRoot))
                    {
                        Console.Write("ERROR: Directory {0} does not exist", config.LocalFolderRoot);
                        Environment.ExitCode = ERROR_BAD_ARGUMENTS;
                        return;
                    }
                }
                if (action == null)
                {
                    action = "u";
                }
                try
                {
                    if (config.TbbExtensions == null)
                    {
                        config.TbbExtensions = new List<string> { "cshtml" };
                    }
                    var uploadSet = new TemplateSyncSet(config);
                    if (action.Contains("u"))
                    {
                        uploadSet.UploadChanged();
                    }
                    if (action.Contains("d"))
                    {
                        uploadSet.OverwriteFromCms();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                    Environment.ExitCode = 1;
                }
            }
        }
        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: TcmTemplateSync [options] [LocalFilePath]");
            Console.WriteLine("Synchronizes TBBs from your local file system to Tridion and back");
            Console.WriteLine();
            Console.WriteLine("  LocalFilePath\tThe location where your TBB files are stored locally");
            Console.WriteLine();
            Console.WriteLine("Basic Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
