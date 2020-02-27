﻿using Sitecore;
using Sitecore.Caching;
using Sitecore.Configuration;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Links;
using Sitecore.Publishing;
using Sitecore.Sites;
using Sitecore.Web;
using Sitecore.XA.Foundation.Multisite.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Sitecore.Support.XA.Foundation.Multisite.EventHandlers
{
    public class HtmlCacheClearer : Sitecore.XA.Foundation.Multisite.EventHandlers.HtmlCacheClearer
    {
        private readonly IEnumerable<ID> _fieldIds;

        public HtmlCacheClearer() : base()
        {
            IEnumerable<XmlNode> source = Factory.GetConfigNodes("experienceAccelerator/multisite/htmlCacheClearer/fieldID").Cast<XmlNode>();
            _fieldIds = from node in source
                        select new ID(node.InnerText);
        }

        public new void OnPublishEnd(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            SitecoreEventArgs sitecoreEventArgs = args as SitecoreEventArgs;
            if (sitecoreEventArgs != null)
            {
                Publisher publisher = sitecoreEventArgs.Parameters[0] as Publisher;
                if (publisher != null)
                {
                    if (publisher.Options.RootItem != null)
                    {
                        List<SiteInfo> usages = GetUsages(publisher.Options.RootItem);
                        if (usages.Count > 0)
                        {
                            usages.ForEach(ClearSiteCache);
                            return;
                        }
                    }
                    #region Removed code commit_from_bug_#12567
                    //else
                    //{
                    //    foreach (Site item in from site in SiteManager.GetSites()
                    //                          where site.IsSxaSite()
                    //                          select site)
                    //    {
                    //        ClearSiteCache(item.Name);
                    //    }
                    //}
                    #endregion
                }
            }
            ClearCache(sender, args);
            #region Added code commit_from_bug_#12567
            ClearAllSxaSitesCaches();
            #endregion
        }

        protected virtual void ClearAllSxaSitesCaches()
        {
            SiteManager.GetSites().Where(site => site.IsSxaSite()).Select(site => site.Name).ForEach(ClearSiteCache);
        }

        public new void OnPublishEndRemote(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            PublishEndRemoteEventArgs publishEndRemoteEventArgs = args as PublishEndRemoteEventArgs;
            if (publishEndRemoteEventArgs != null)
            {
                Item item = Factory.GetDatabase(publishEndRemoteEventArgs.SourceDatabaseName, assert: false)?.GetItem(new ID(publishEndRemoteEventArgs.RootItemId));
                if (item != null)
                {
                    List<SiteInfo> usages = GetUsages(item);
                    if (usages.Count > 0)
                    {
                        usages.ForEach(ClearSiteCache);
                        return;
                    }
                }
            }
            ClearCache(sender, args);
            #region Added code commit_from_bug_#12567
            ClearAllSxaSitesCaches();
            #endregion
        }

        private void ClearSiteCache(string siteName)
        {
            Log.Info($"HtmlCacheClearer clearing cache for {siteName} site", this);
            ProcessSite(siteName);
            Log.Info("HtmlCacheClearer done.", this);
        }

        private void ClearSiteCache(SiteInfo site)
        {
            ClearSiteCache(site.Name);
        }

        private void ProcessSite(string siteName)
        {
            SiteContext site = Factory.GetSite(siteName);
            if (site != null)
            {
                CacheManager.GetHtmlCache(site)?.Clear();
            }
        }

        private List<SiteInfo> GetUsages(Item item)
        {
            Assert.IsNotNull(item, "item");
            List<SiteInfo> list = new List<SiteInfo>();
            Item item2 = item;
            do
            {
                #region Removed code commit_from_bug_#290325
                //if (MultisiteContext.GetSiteItem(item2) != null)
                //{
                //    SiteInfo siteInfo = SiteInfoResolver.GetSiteInfo(item2);
                //    if (siteInfo != null)
                //    {
                //        list.Add(siteInfo);
                //        break;
                //    }
                //}
                #endregion
                ItemLink[] itemReferrers = Globals.LinkDatabase.GetItemReferrers(item2, includeStandardValuesLinks: false);
                foreach (ItemLink itemLink in itemReferrers)
                {
                    if (IsOneOfWanted(itemLink.SourceFieldID))
                    {
                        Item sourceItem = itemLink.GetSourceItem();
                        SiteInfo siteInfo2 = SiteInfoResolver.GetSiteInfo(sourceItem);
                        list.Add(siteInfo2);
                    }
                }
                item2 = item2.Parent;
            }
            while (item2 != null);
            list = (from s in list
                    where s != null
                    select s into g
                    group g by new
                    {
                        g.Name
                    } into x
                    select x.First()).ToList();
            list.AddRange(GetAllSitesForSharedSites(list));
            return list;
        }

        private bool IsOneOfWanted(ID sourceFieldId)
        {
            return _fieldIds.Any((ID x) => x.Equals(sourceFieldId));
        }
    }

}