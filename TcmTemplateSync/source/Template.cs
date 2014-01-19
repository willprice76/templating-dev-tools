using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tridion.Extensions.Deployment.TemplateUpload
{
    /// <summary>
    /// Encapsulates data about a TBB
    /// </summary>
    public class Template
    {
        public Template()
        {
        }

        public Template(FileInfo file, string path)
        {
            int pos = file.Name.LastIndexOf(".");
            this.Type = Template.GetTbbType(file.Name.Substring(pos + 1));
            this.Title = file.Name.Substring(0,pos);
            this.LastModified = file.LastWriteTime;
            this.Content = File.ReadAllText(file.FullName);
            this.RelativeWebdavUrl = path + file.Name;
        }
        public string Type { get; set; }
        public string Title { get; set; }
        public string RelativeWebdavUrl { get; set; }
        public string TcmUri { get; set; }
        public DateTime LastModified { get; set; }
        public TemplateStatus Status { get; set; }
        public string Content { get; set; }

        public string FolderUri { get; set; }

        public static string GetTbbExtension(string subType)
        {
            switch (subType)
            {
                case "8":
                    return "cshtml";
                case "7":
                    return "dwt";
                case "10":
                    return "xslt";
                case "6":
                    return "tbbcs";
                default:
                    return null;
            }
        }

        public static string GetTbbType(string extension)
        {
            switch (extension.ToLower())
            {
                case "cshtml":
                    return "RazorTemplate";
                case "dwt":
                    return "DreamweaverTemplate";
                case "xslt":
                    return "XsltTemplate";
                case "tbbcs":
                    return "CSharpTemplate";
                default:
                    return null;
            }
        }
    }

    public enum TemplateStatus
    {
        Unchanged,
        Modified,
        New
    }
}
