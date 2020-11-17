﻿using Serenity.Abstractions;
using Serenity.ComponentModel;
using Serenity.Extensibility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Serenity.Reporting
{
    public class ReportRegistry
    {
        private Dictionary<string, Report> reportByKey;
        private Dictionary<string, List<Report>> reportsByCategory;
        private readonly IEnumerable<Type> types;
        private readonly IPermissionService permissions;
        private readonly ITextLocalizer localizer;

        public ReportRegistry(IEnumerable<Type> types, IPermissionService permissions, ITextLocalizer localizer)
        {
            this.types = types ?? throw new ArgumentNullException(nameof(types));
            this.permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            this.localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        public static string GetReportKey(Type type)
        {
            var attr = type.GetCustomAttribute<ReportAttribute>(false);
            if (attr == null || attr.ReportKey.IsNullOrEmpty())
                return type.FullName;

            return attr.ReportKey;
        }

        private static string GetReportCategory(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(CategoryAttribute), false);
            if (attr.Length == 1)
                return ((CategoryAttribute)attr[0]).Category;

            return String.Empty;
        }

        public static string GetReportCategoryTitle(string key, ITextLocalizer localizer)
        {
            var title = localizer?.TryGet("Report.Category." + key.Replace("/", "."));
            if (title == null)
            {
                key = key ?? "";
                var idx = key.LastIndexOf('/');
                if (idx >= 0 && idx < key.Length - 1)
                    key = key.Substring(idx + 1);
                return key;
            }

            return title;
        }

        private void EnsureTypes()
        {
            if (reportsByCategory != null)
                return;

            var reportByKeyNew = new Dictionary<string, Report>();
            var reportsByCategoryNew = new Dictionary<string, List<Report>>();

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<ReportAttribute>(false);
                if (attr != null)
                {
                    var report = new Report(type, localizer);
                    var key = report.Key.TrimToNull() ?? type.FullName;

                    reportByKeyNew[key] = report;

                    var category = report.Category.Key;
                    List<Report> reports;

                    if (!reportsByCategoryNew.TryGetValue(category, out reports))
                    {
                        reports = new List<Report>();
                        reportsByCategoryNew[category] = reports;
                    }

                    reports.Add(report);
                }
            }

            reportsByCategory = reportsByCategoryNew;
            reportByKey = reportByKeyNew;
        }

        public bool HasAvailableReportsInCategory(string categoryKey)
        {
            EnsureTypes();

            List<Report> reports;
            if (!reportsByCategory.TryGetValue(categoryKey, out reports))
                return false;

            foreach (var report in reports)
                if (report.Permission == null || permissions.HasPermission(report.Permission))
                    return true;

            return false;
        }

        public IEnumerable<Report> GetAvailableReportsInCategory(string categoryKey)
        {
            EnsureTypes();

            var list = new List<Report>();

            foreach (var k in reportsByCategory)
                if (categoryKey.IsNullOrEmpty() ||
                    String.Compare(k.Key, categoryKey, StringComparison.OrdinalIgnoreCase) == 0 ||
                    (k.Key + "/").StartsWith((categoryKey ?? ""), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var report in k.Value)
                        if (report.Permission == null || permissions.HasPermission(report.Permission))
                        {
                            list.Add(report);
                        }
                }

            list.Sort((x, y) => (x.Title ?? "").CompareTo(y.Title ?? ""));

            return list;
        }

        public Report GetReport(string reportKey, bool validatePermission = true)
        {
            EnsureTypes();

            if (reportByKey.IsEmptyOrNull())
                throw new ArgumentNullException("reportKey");

            Report report;
            if (reportByKey.TryGetValue(reportKey, out report))
            {
                if (validatePermission && report.Permission != null)
                    permissions.ValidatePermission(report.Permission, localizer);

                return report;
            }

            return null;
        }

        public class Report
        {
            public Type Type { get; private set; }
            public string Key { get; private set; }
            public string Permission { get; private set; }
            public string Title { get; private set; }
            public Category Category { get; private set; }

            public Report(Type type, ITextLocalizer localizer)
            {
                if (type == null)
                    throw new ArgumentNullException("type");

                this.Type = type;

                this.Key = GetReportKey(type);

                var attr = type.GetCustomAttributes(typeof(DisplayNameAttribute), false);
                if (attr.Length == 1)
                    this.Title = ((DisplayNameAttribute)attr[0]).DisplayName;

                var category = GetReportCategory(type);
                this.Category = new ReportRegistry.Category(category, GetReportCategoryTitle(category, localizer));

                attr = type.GetCustomAttributes(typeof(RequiredPermissionAttribute), false);
                if (attr.Length > 0)
                    this.Permission = ((RequiredPermissionAttribute)attr[0]).Permission ?? "?";
            }
        }

        public class Category
        {
            public string Key { get; private set; }
            public string Title { get; private set; }

            public Category(string key, string title)
            {
                this.Key = key;
                this.Title = title;
            }
        }
    }
}