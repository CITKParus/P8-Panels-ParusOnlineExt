﻿using ClientModelsPrivate.Main.Menus;
using ExtensionModules.Interfaces;
using ExtensionModules.Interfaces.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using FlowControl.Builder.Elements;
using ParusClient.Activities;
using System.Xml;
using System.IO;
using CommonActivities.Activities;

namespace P8PanelsParusOnlineExt
{

    public class P8PanelConfig
    {
        private List<P8PanelMenuApp> _menuApps = new List<P8PanelMenuApp>();

        private List<P8PanelMenuItem> _menuItems = new List<P8PanelMenuItem>();

        private List<P8Panel> _panels = new List<P8Panel>();

        private string _panelsUrlBase;

        public P8PanelConfig(string confiFileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(confiFileName);
            XmlNode section = doc.DocumentElement.SelectSingleNode("/CITK.P8Panels");
            foreach (XmlNode sectionNode in section.ChildNodes)
            {
                if (sectionNode.Name == "MenuItems")
                {
                    foreach (XmlNode menuAppNode in sectionNode.ChildNodes)
                    {
                        _menuApps.Add(new P8PanelMenuApp() { name = menuAppNode.Attributes["name"].Value });
                        foreach (XmlNode menuItemsNode in menuAppNode.ChildNodes)
                        {
                            _menuItems.Add(new P8PanelMenuItem()
                            {
                                app = menuAppNode.Attributes["name"].Value,
                                parent = menuItemsNode.Attributes["parent"].Value,
                                separator = menuItemsNode.Attributes["separator"] != null ? bool.Parse(menuItemsNode.Attributes["separator"].Value) : false,
                                name = menuItemsNode.Attributes["name"]?.Value,
                                caption = menuItemsNode.Attributes["caption"]?.Value,
                                url = menuItemsNode.Attributes["url"]?.Value,
                                panelName = menuItemsNode.Attributes["panelName"]?.Value
                            });
                        }
                    }
                }
                if (sectionNode.Name == "Panels")
                {
                    _panelsUrlBase = sectionNode.Attributes["urlBase"].Value;
                    foreach (XmlNode panelNode in sectionNode.ChildNodes)
                    {
                        _panels.Add(new P8Panel()
                        {
                            name = panelNode.Attributes["name"].Value,
                            caption = panelNode.Attributes["caption"].Value,
                            url = panelNode.Attributes["url"].Value,
                            path = panelNode.Attributes["path"].Value,
                            icon = panelNode.Attributes["icon"].Value,
                            showInPanelsList = panelNode.Attributes["showInPanelsList"] != null ? bool.Parse(panelNode.Attributes["showInPanelsList"].Value) : false,
                        });
                    }
                }
            }
        }

        public P8Panel FindPanelByName(string name)
        {
            return _panels.Find(panel => panel.name == name);
        }

        public List<P8PanelMenuApp> menuApps { get => _menuApps; }

        public List<P8PanelMenuItem> menuItems { get => _menuItems; }

        public List<P8Panel> panels { get => _panels; }

        public string panelsUrlBase { get => _panelsUrlBase; }
    }

    public class P8PanelMenuApp
    {
        public string name { get; set; }
    }

    public class P8PanelMenuItem
    {
        public string app { get; set; }
        public string parent { get; set; }
        public bool separator { get; set; }
        public string name { get; set; }
        public string caption { get; set; }
        public string url { get; set; }
        public string panelName { get; set; }
    }

    public class P8Panel
    {
        public string name { get; set; }
        public string caption { get; set; }
        public string url { get; set; }
        public string path { get; set; }
        public string icon { get; set; }
        public bool showInPanelsList { get; set; }
    }

    public class Module : ExtensionModuleBase
    {

        private static string _configFile = Path.GetDirectoryName(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile) + "\\Config\\p8panels.config";

        private static IList<IHook> _hooks = new List<IHook>();

        public override string ModuleName => "P8Panels";

        public override string AuthorInfo => "ЦИТК";

        public override IList<IHook> Hooks => _hooks;

        public override bool HasViews => false;

        public Module()
        {
            P8PanelConfig pconf = new P8PanelConfig(_configFile);
            pconf.menuApps.ForEach(menuApp => {
                _hooks.Add(MainMenuProcessorHook.Make(menuApp.name, mainMenu => {
                    pconf.menuItems.ForEach(menuItem => {
                        if (menuItem.app == menuApp.name)
                        {
                            var parentMenuItem = mainMenu.Root?.Children?.First(x => x.Value?.Name == menuItem.parent);
                            parentMenuItem.Children.Add(new MainMenuItemNode { Value = new MainMenuItem { IsSeparator = menuItem.separator, Name = menuItem.name, Caption = menuItem.caption } });
                        }
                    });
                    return mainMenu;
                }));
            });
            Dictionary<string, Func<Sequence>> menuItemsActions = new Dictionary<string, Func<Sequence>>();
            pconf.menuItems.ForEach(menuItem => {
                if (!menuItem.separator)
                    if (menuItem.panelName == null)
                        menuItemsActions.Add(menuItem.name, () => new Sequence().Add<OpenExternalLinkActivity>(new { caption = menuItem.caption, url = menuItem.url }));
                    else
                    {
                        P8Panel panel = pconf.FindPanelByName(name: menuItem.panelName);
                        if (panel != null)
                        {
                            string panelUrl = pconf.panelsUrlBase;
                            if (!panelUrl.EndsWith("/") && !panel.url.StartsWith("/")) panelUrl += "/";
                            panelUrl += panel.url;
                            menuItemsActions.Add(menuItem.name, () => new Sequence().Add<OpenExternalLinkActivity>(new { caption = panel.caption, url = panelUrl }));
                        }
                        else
                            menuItemsActions.Add(menuItem.name, () => new Sequence().Add<MessageActivity>(new { caption = "Панель не определена", text = $"Панель \"{menuItem.panelName}\" не определена." }));
                    }

            });
            _hooks.Add(MainMenuItemBuilderHook.Make(menuItemsActions));
        }

        public static string configFile { get => _configFile; }
    }
}
