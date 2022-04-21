﻿namespace Sitecore.Foundation.SitecoreExtensions.MVC.Extensions
{
	public class AppEnvSettingsModel
	{
		public bool IsNonProdEnv { get; set; }
		public bool EnableCustomFileLogging { get; set; }
		public string AppLogFileKey { get; set; } = string.Empty;
	}
}