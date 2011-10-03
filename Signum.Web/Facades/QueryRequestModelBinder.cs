﻿#region usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Collections.Specialized;
using Signum.Utilities;
using Signum.Entities.DynamicQuery;
using Signum.Web.Properties;
using Signum.Engine.DynamicQuery;
using Signum.Entities;
using System.Web;
using Signum.Entities.Reflection;
using Signum.Utilities.Reflection;
using System.Text.RegularExpressions;
using Signum.Engine;
#endregion

namespace Signum.Web
{
    public class QueryRequestModelBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            QueryRequest qr = new QueryRequest();

            NameValueCollection parameters = controllerContext.HttpContext.Request.Params;

            if (parameters.AllKeys.Any(name => !name.HasText()))
                throw new Exception("Incorrect URL: " + controllerContext.HttpContext.Request.Url.ToString());

            string webQueryName = "";
            object rawValue = bindingContext.ValueProvider.GetValue("webQueryName").TryCC(vp => vp.RawValue);
            if (rawValue.GetType() == typeof(string[]))
                webQueryName = ((string[])rawValue)[0];
            else 
                webQueryName = (string)rawValue;

            if (!webQueryName.HasText())
                throw new InvalidOperationException("webQueryName not provided");

            qr.QueryName = Navigator.ResolveQueryName(webQueryName);

            QueryDescription queryDescription = DynamicQueryManager.Current.QueryDescription(qr.QueryName);

            qr.Filters = ExtractFilterOptions(controllerContext.HttpContext, queryDescription);
            qr.Orders = ExtractOrderOptions(controllerContext.HttpContext, queryDescription);
            qr.Columns = ExtractColumnsOptions(controllerContext.HttpContext, queryDescription);

            if (parameters.AllKeys.Contains("elems"))
            {
                int elems;
                if (int.TryParse(parameters["elems"], out elems))
                    qr.ElementsPerPage = elems;
            }

            if (parameters.AllKeys.Contains("page"))
            {
                int page;
                if (int.TryParse(parameters["page"], out page))
                    qr.CurrentPage = page;
            }
            else
            {
                qr.CurrentPage = 0;
            }

            return qr;
        }

        public static List<Signum.Entities.DynamicQuery.Filter> ExtractFilterOptions(HttpContextBase httpContext, QueryDescription queryDescription)
        {
            List<Signum.Entities.DynamicQuery.Filter> result = new List<Signum.Entities.DynamicQuery.Filter>();

            NameValueCollection parameters = httpContext.Request.Params;
            
            string field = parameters["filters"];

            if (!field.HasText())
                return result;

            var matches = FindOptionsModelBinder.FilterRegex.Matches(field).Cast<Match>();

            return matches.Select(m =>
            {
                string name = m.Groups["token"].Value;
                var token = QueryUtils.Parse(name, queryDescription);
                return new Signum.Entities.DynamicQuery.Filter
                {
                    Token = token,
                    Operation = EnumExtensions.ToEnum<FilterOperation>(m.Groups["op"].Value),
                    Value = FindOptionsModelBinder.Convert(FindOptionsModelBinder.DecodeValue(m.Groups["value"].Value), token.Type)
                };
            }).ToList();
        }

        public static List<Order> ExtractOrderOptions(HttpContextBase httpContext, QueryDescription queryDescription)
        {
            List<Order> result = new List<Order>();

            NameValueCollection parameters = httpContext.Request.Params;
            string field = parameters["orders"];
            
            if (!field.HasText())
                return result;

            var matches = FindOptionsModelBinder.OrderRegex.Matches(field).Cast<Match>();

            return matches.Select(m =>
            {
                var tokenCapture = m.Groups["token"].Value;
                
                OrderType orderType = tokenCapture.StartsWith("-") ? OrderType.Descending : OrderType.Ascending;
                string token = orderType == OrderType.Ascending ? tokenCapture : tokenCapture.Substring(1, tokenCapture.Length - 1);
                
                return new Order(QueryUtils.Parse(token, queryDescription), orderType);
            }).ToList();
        }

        public static List<Column> ExtractColumnsOptions(HttpContextBase httpContext, QueryDescription queryDescription)
        {
            List<Column> result = new List<Column>();

            NameValueCollection parameters = httpContext.Request.Params;
            string field = parameters["columns"];
            
            if (!field.HasText())
                return result;

            var matches = FindOptionsModelBinder.ColumnRegex.Matches(field).Cast<Match>();

            return matches.Select(m =>
            {
                var colName = m.Groups["token"].Value;
                var displayCapture = m.Groups["name"].Captures;
                string displayName = displayCapture.Count > 0 ? FindOptionsModelBinder.DecodeValue(m.Groups["name"].Value) : colName;
                
                return new Column(QueryUtils.Parse(colName, queryDescription), displayName);
            }).ToList();
        }
    }
}
