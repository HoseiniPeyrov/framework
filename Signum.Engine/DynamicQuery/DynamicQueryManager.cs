﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Utilities;
using System.Data;
using System.Reflection;
using Signum.Entities.DynamicQuery;
using Signum.Engine.Properties;
using Signum.Entities;
using System.Diagnostics;

namespace Signum.Engine.DynamicQuery
{
    public class DynamicQueryManager
    {
        internal DynamicQueryManager Parent{get; private set;}
        Dictionary<object, IDynamicQuery> queries = new Dictionary<object, IDynamicQuery>();

        public DynamicQueryManager(DynamicQueryManager parent)
        {
            this.Parent = parent;         
        }

        public IDynamicQuery this[object queryName]
        {
            get
            {
                return TryGet(queryName).ThrowIfNullC(Resources.TheView0IsNotOnQueryManager.Formato(queryName));
            }
            set {

                string error = value.GetErrors();

                if (error.HasText())
                    Debug.WriteLine("Query {0} -> {1}".Formato(Signum.Entities.DynamicQuery.QueryUtils.GetQueryName(queryName), error));

                queries[queryName] = value;
            }
        }

        IDynamicQuery TryGet(object queryName)
        {
            var result = queries.TryGetC(queryName);
            if (result != null)
                return result;

            if (Parent != null)
                return Parent[queryName];

            return null; 
        }

        public QueryResult ExecuteQuery(object queryName, List<Filter> filter, int? limit)
        {
            return this[queryName].ExecuteQuery(filter, limit);
        }


        public int ExecuteQueryCount(object queryName, List<Filter> filters)
        {
            return this[queryName].ExecuteQueryCount(filters);
        }

        public QueryDescription QueryDescription(object queryName)
        {
            return this[queryName].GetDescription();
        }

        public List<object> GetQueryNames()
        {
            if (Parent == null)
                return queries.Keys.ToList();

            return queries.Keys.Union(Parent.GetQueryNames()).ToList(); 
        }

        public string Errors(object queryName)
        {
            try
            {
                IDynamicQuery dq = this[queryName];

                string error = dq.GetErrors();
                if (error.HasText())
                    return "Error {0}: No ToLite() on {1}".Formato(queryName, error);

                Connection.CommandCount = 0;
                QueryResult result = dq.ExecuteQuery(new List<Filter>(), 100);

                if (Connection.CommandCount != 1)
                    return "Error {0}: N + 1 query problem".Formato(queryName);

                return null;
            }
            catch (Exception e)
            {
                return "Error {0}: {1}".Formato(queryName, e.Message);
            }
        }
    }
}
