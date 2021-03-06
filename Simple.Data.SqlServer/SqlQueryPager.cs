﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Simple.Data.Ado;

namespace Simple.Data.SqlServer
{
    [Export(typeof(IQueryPager))]
    public class SqlQueryPager : IQueryPager
    {
        private static readonly Regex ColumnExtract = new Regex(@"SELECT\s*(.*)\s*(FROM.*)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex ColumnDistinctExtract = new Regex(@"SELECT\s*DISTINCT\s*(.*)\s*(FROM.*)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex SelectDistinctMatch = new Regex(@"^SELECT\s*DISTINCT\s*", RegexOptions.IgnoreCase);
        private static readonly Regex SelectMatch = new Regex(@"^SELECT\s*", RegexOptions.IgnoreCase);
        
        public IEnumerable<string> ApplyLimit(string sql, int take)
        {
            yield return (SelectDistinctMatch.IsMatch(sql)
                ? SelectDistinctMatch.Replace(sql, match => match.Value + " TOP " + take + " ")
                : SelectMatch.Replace(sql, match => match.Value + " TOP " + take + " "));
        }
        
        public IEnumerable<string> ApplyPaging(string sql, int skip, int take)
        {
            var builder = new StringBuilder("WITH __Data AS (SELECT ");
            var match = (ColumnDistinctExtract.IsMatch(sql) ? ColumnDistinctExtract.Match(sql) : ColumnExtract.Match(sql));
            var columns = match.Groups[1].Value.Trim();
            var fromEtc = match.Groups[2].Value.Trim();

            builder.Append(columns);
            
            var orderBy = ExtractOrderBy(columns, ref fromEtc);
            
            builder.AppendFormat(", ROW_NUMBER() OVER({0}) AS [_#_]", orderBy);
            builder.AppendLine();
            builder.Append(fromEtc);
            builder.AppendLine(")");
            builder.AppendFormat("SELECT {0} FROM __Data WHERE [_#_] BETWEEN {1} AND {2}", DequalifyColumns(columns),
                                 skip + 1, skip + take);
            yield return builder.ToString();
        }

        private static string DequalifyColumns(string original)
        {
            var q = from part in original.Split(',')
                    select part.Substring(Math.Max(part.LastIndexOf('.') + 1, part.LastIndexOf('[')));
            return string.Join(",", q);
        }

        private static string ExtractOrderBy(string columns, ref string fromEtc)
        {
            string orderBy;
            int index = fromEtc.IndexOf("ORDER BY", StringComparison.InvariantCultureIgnoreCase);
            if (index > -1)
            {
                orderBy = fromEtc.Substring(index).Trim();
                fromEtc = fromEtc.Remove(index).Trim();
            }
            else
            {
                orderBy = "ORDER BY " + columns.Split(',').First().Trim();

                var aliasIndex = orderBy.IndexOf(" AS [", StringComparison.InvariantCultureIgnoreCase);

                if (aliasIndex > -1)
                {
                    orderBy = orderBy.Substring(0, aliasIndex);
                }
            }
            return orderBy;
        }
    }
}
