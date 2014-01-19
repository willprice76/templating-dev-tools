using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tridion.Extensions.Deployment.TemplateUpload
{
    /// <summary>
    /// Configuration settings for sync
    /// </summary>
    public class Config
    {
        public string LocalFolderRoot { get; set; }
        public string RootFolderUri { get; set; }
        public List<string> TbbExtensions { get; set; }
        public string TargetUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        //timeoffset local time / server time
        public double TimeOffSet { get; set; }
    }
}
