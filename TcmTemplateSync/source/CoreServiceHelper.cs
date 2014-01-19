using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.IO;
using System.Xml.Linq;
using Tridion.ContentManager.CoreService.Client;
using System.Web;
using System.Net;
using System.Xml;
using System.ServiceModel.Channels;

namespace Tridion.Extensions.Deployment.TemplateUpload
{
    /// <summary>
    /// Single point for core service access
    /// </summary>
    public class CoreServiceHelper : IDisposable
    {
        private static int _messageSize = 2147483647;
        private SessionAwareCoreServiceClient _sessionAwareClient = null;
        private CoreServiceClient _client = null;
        public CoreServiceHelper()
            : this(null)
        { }

        public CoreServiceHelper(string endpointUrl, string user=null, string password=null)
        {
            Binding binding = GetBindingFromUrl(endpointUrl);
            var endpoint = new EndpointAddress(endpointUrl);
            if (binding is NetTcpBinding || binding is WSHttpBinding)
            {
                _sessionAwareClient = new SessionAwareCoreServiceClient(binding, endpoint);
                if (user != null)
                {
                    _sessionAwareClient.ChannelFactory.Credentials.Windows.ClientCredential = new NetworkCredential(user, password);
                }
            }
            else
            {
                _client = new CoreServiceClient(binding, endpoint);
                if (user != null)
                {
                    _client.ChannelFactory.Credentials.Windows.ClientCredential = new NetworkCredential(user, password);
                }
            }
        }

        private Binding GetBindingFromUrl(string endpointUrl)
        {
            if (endpointUrl.ToLower().StartsWith("net.tcp://"))
            {
                return new NetTcpBinding
                {
                    MaxReceivedMessageSize = _messageSize,
                    ReaderQuotas = new XmlDictionaryReaderQuotas
                    {
                        MaxStringContentLength = _messageSize,
                        MaxArrayLength = _messageSize
                    }
                };
            }
            else
            {
                if (endpointUrl.ToLower().Contains("wshttp"))
                {
                    return new WSHttpBinding
                    {
                        MaxReceivedMessageSize = _messageSize,
                        ReaderQuotas = new XmlDictionaryReaderQuotas
                        {
                            MaxStringContentLength = _messageSize,
                            MaxArrayLength = _messageSize
                        },
                        Security = new WSHttpSecurity()
                        {
                            Mode = SecurityMode.Message
                        }
                    };
                }
                else
                {
                    //assume basic http
                    return new BasicHttpBinding
                    {
                        MaxReceivedMessageSize = _messageSize,
                        ReaderQuotas = new XmlDictionaryReaderQuotas
                        {
                            MaxStringContentLength = _messageSize,
                            MaxArrayLength = _messageSize
                        },
                        Security = new BasicHttpSecurity()
                        {
                            Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                            Transport = new HttpTransportSecurity()
                            {
                                ClientCredentialType = HttpClientCredentialType.Windows,
                            }
                        }
                    };
                }
            }
            
        }

        public void Dispose()
        {
            if (_client != null)
            {
                if (_client.State == CommunicationState.Faulted)
                {
                    _client.Abort();
                }
                else
                {
                    _client.Close();
                }
            }
            if (_sessionAwareClient != null)
            {
                if (_sessionAwareClient.State == CommunicationState.Faulted)
                {
                    _sessionAwareClient.Abort();
                }
                else
                {
                    _sessionAwareClient.Close();
                }
            }
        }
        public IdentifiableObjectData Create(IdentifiableObjectData data)
        {
            return _client != null ? this._client.Create(data, new ReadOptions()) : this._sessionAwareClient.Create(data, new ReadOptions());
        }
        public IdentifiableObjectData Save(IdentifiableObjectData data)
        {
            return _client != null ? this._client.Save(data, new ReadOptions()) : this._sessionAwareClient.Save(data, new ReadOptions());
        }
        public IdentifiableObjectData Read(string uri)
        {
            return _client != null ? this._client.Read(uri, new ReadOptions()) : this._sessionAwareClient.Read(uri, new ReadOptions());
        }
        public void Delete(string uri)
        {
            if (_client != null) 
            {
                this._client.Delete(uri);
            }
            else
            {
                this._sessionAwareClient.Delete(uri);
            }
        }
        public VersionedItemData CheckOut(string uri)
        {
            return _client != null ? this._client.CheckOut(uri, false, new ReadOptions()) : this._sessionAwareClient.CheckOut(uri, false, new ReadOptions());
        }
        public VersionedItemData CheckIn(string uri)
        {
            return _client != null ? this._client.CheckIn(uri, new ReadOptions()) : this._sessionAwareClient.CheckIn(uri, new ReadOptions());
        }

        public TemplateBuildingBlockData CreateTemplateBuildingBlock(string title, string folderUri, string content, string templateType)
        {
            var tbb = new TemplateBuildingBlockData
            {
                LocationInfo = new LocationInfo
                {
                    OrganizationalItem = new LinkToOrganizationalItemData
                    {
                        IdRef = folderUri
                    },
                },
                TemplateType = templateType,
                Content = content,
                Title = title,
                Id = "tcm:0-0-0"
            };
            return (TemplateBuildingBlockData)Create(tbb);
        }


        public bool UpdateTemplateBuildingBlock(string tbbUri, string content)
        {
            TemplateBuildingBlockData tbb = (TemplateBuildingBlockData)Read(tbbUri);
            if (tbb.Content != content)
            {
                bool checkedOut = tbb.LockInfo.LockType.Value.HasFlag(LockType.CheckedOut);
                if (checkedOut)
                {
                    throw new Exception(String.Format("TBB {0} ({1}) is checked out by {2}", tbb.Title, tbb.Id, tbb.LockInfo.LockUser.Title));
                }
                try
                {
                    tbb = (TemplateBuildingBlockData)CheckOut(tbbUri);
                    tbb.Content = content;
                    tbb = (TemplateBuildingBlockData)Save(tbb);
                    return true;
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    CheckIn(tbbUri);
                }
            }
            return false;
        }

		public IEnumerable<XElement> GetOrgItemContents(string orgItemUri, List<ItemType> types, bool recursive = false, ListBaseColumns? cols = null)
		{
			OrganizationalItemItemsFilterData filter = new OrganizationalItemItemsFilterData();
			filter.ItemTypes = types.ToArray();
			filter.Recursive = recursive;
			filter.BaseColumns = cols==null ? ListBaseColumns.IdAndTitle : cols;
            var res = _client != null ? this._client.GetListXml(orgItemUri, filter) : this._sessionAwareClient.GetListXml(orgItemUri, filter);
			return res.Elements();
		}
		public IdentifiableObjectData GetItem(string itemUri)
		{
            return Read(itemUri);
		}
        public FolderData CreateFolder(string parentUri, string name)
        {
            var folder = new FolderData
            {
                LocationInfo = new LocationInfo
                {
                    OrganizationalItem = new LinkToOrganizationalItemData
                    {
                        IdRef = parentUri
                    },
                },
                Title = name,
                Id = "tcm:0-0-0"
            };
            return (FolderData)Create(folder);
        }
    }
}

