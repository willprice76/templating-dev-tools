using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tridion.ContentManager.CoreService.Client;

namespace Tridion.Extensions.Deployment.TemplateUpload
{
    /// <summary>
    /// A set of templates to sync, and methods to sync them
    /// </summary>
    public class TemplateSyncSet
    {
        private Config _config;
        private CoreServiceHelper _client;
        private Dictionary<string, Template> _localTemplates = new Dictionary<string, Template>();
        private Dictionary<string, Template> _cmsTemplates = new Dictionary<string, Template>();
        private Dictionary<string, string> _templateFolders = new Dictionary<string, string>();
        private List<string> _missingDirectories = new List<string>();
        
        public TemplateSyncSet(Config config)
        {
            var root = new DirectoryInfo(config.LocalFolderRoot);
            _config = config;
            _client = new CoreServiceHelper(config.TargetUrl,config.Username,config.Password);
            _config.TimeOffSet = GetTimeOffSet();
            LoadTemplateList(_config.RootFolderUri);
            ProcessDirectory(root);
        }

        private double GetTimeOffSet()
        {
            //Create and delete a test DWT TBB to get server time and see how it differs with local time
            var test = _client.CreateTemplateBuildingBlock(DateTime.Now.Ticks.ToString(), _config.RootFolderUri, "This TBB is to test server time", Template.GetTbbType("dwt"));
            TimeSpan diff = (DateTime)test.VersionInfo.CreationDate - DateTime.Now;
            _client.Delete(test.Id);
            if (Math.Abs(diff.TotalSeconds) > 1)
            {
                return diff.TotalSeconds;
            }
            return 0;
        }

        private void LoadTemplateList(string folderUri, string rootPath = "/")
        {
            if (!Directory.Exists(_config.LocalFolderRoot + rootPath))
            {
                _missingDirectories.Add(_config.LocalFolderRoot + rootPath);
            }
            FolderData folder = (FolderData)_client.GetItem(folderUri);
            _templateFolders.Add(rootPath, folder.Id);
            foreach (var tbbNode in _client.GetOrgItemContents(folderUri, new List<ItemType> { ItemType.TemplateBuildingBlock },false,ListBaseColumns.Extended))
            {
                var extension = Template.GetTbbExtension(tbbNode.Attribute("SubType").Value);
                if (extension!=null && _config.TbbExtensions.Contains(extension))
                {
                    var title = tbbNode.Attribute("Title").Value;
                    _cmsTemplates.Add(rootPath + title + "." + extension, new Template { Title = title, TcmUri = tbbNode.Attribute("ID").Value, LastModified = DateTime.Parse(tbbNode.Attribute("Modified").Value) });
                }
            }
            foreach (var folderNode in _client.GetOrgItemContents(folder.Id, new List<ItemType> { ItemType.Folder }))
            {
                LoadTemplateList(folderNode.Attribute("ID").Value, rootPath + folderNode.Attribute("Title").Value + "/");
            }
        }

        private void ProcessDirectory(DirectoryInfo dir, string rootPath = "/")
        {
            foreach (var extension in _config.TbbExtensions)
            {
                foreach (var file in dir.GetFiles("*."+extension))
                {
                    ProcessFile(file, rootPath);
                }
            }
            foreach (var subDir in dir.GetDirectories())
            {
                var path = rootPath + subDir.Name + "/";
                var parentUri = _templateFolders[rootPath];
                if (!_templateFolders.ContainsKey(path))
                {
                    FolderData folder = _client.CreateFolder(parentUri, subDir.Name);
                    _templateFolders.Add(path, folder.Id);
                } 
                ProcessDirectory(subDir, path);
            }
        }

        private void ProcessFile(FileInfo file, string path)
        {
            var template = new Template(file, path);
            if (_templateFolders.ContainsKey(path))
            {
                template.FolderUri = _templateFolders[path];
            }
            CheckStatus(template);
            _localTemplates.Add(template.RelativeWebdavUrl,template);
        }

        private void CheckStatus(Template template)
        {
            if (_cmsTemplates.ContainsKey(template.RelativeWebdavUrl))
            {
                var cmsTemplate = _cmsTemplates[template.RelativeWebdavUrl];
                template.TcmUri = cmsTemplate.TcmUri;
                TimeSpan diff = cmsTemplate.LastModified - template.LastModified;
                if (diff.TotalSeconds - _config.TimeOffSet > 0)
                {
                    template.Status = TemplateStatus.Unchanged;
                }
                else
                {
                    template.Status = TemplateStatus.Modified;
                }
            }
            else
            {
                template.Status = TemplateStatus.New;
            }
        }

        public void UploadChanged()
        {
            bool success = true;
            //upload new TBBs
            foreach (Template item in _localTemplates.Values.Where(t => t.Status == TemplateStatus.New))
            {
                try
                {
                    _client.CreateTemplateBuildingBlock(item.Title, item.FolderUri, item.Content, item.Type);
                    Console.WriteLine(String.Format("Created {0}", item.RelativeWebdavUrl));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(String.Format("Error creating {0}: {1}", item.RelativeWebdavUrl, ex.Message));
                    success = false;
                }
            }
            //upload modified TBBs
            foreach (Template item in _localTemplates.Values.Where(t => t.Status == TemplateStatus.Modified))
            {
                try
                {
                    if (_client.UpdateTemplateBuildingBlock(item.TcmUri, item.Content))
                    {
                        Console.WriteLine(String.Format("Updated {0}", item.RelativeWebdavUrl));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(String.Format("Error updating {0}: {1}", item.RelativeWebdavUrl, ex.Message));
                    success = false;
                }
            }
            if (!success)
            {
                throw new Exception("Errors occurred when uploading templates. Please check the log/output");
            }
        }

        public void OverwriteFromCms()
        {
            //create missing directories
            foreach (var dir in _missingDirectories)
            {
                Directory.CreateDirectory(dir);
            }
            //download all TBBs
            foreach (var tbbUrl in _cmsTemplates.Keys)
            {
                var template = _cmsTemplates[tbbUrl];
                var data = (TemplateBuildingBlockData)_client.GetItem(template.TcmUri);
                var filepath = _config.LocalFolderRoot + tbbUrl;
                File.WriteAllText(filepath, data.Content);
                File.SetLastWriteTime(filepath, ((DateTime)data.VersionInfo.RevisionDate).AddSeconds(-_config.TimeOffSet));
                Console.WriteLine("Updated local file: " + tbbUrl);
            }
        }
    }
}
