using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TShockAPI;
using TShockAPI.DB;
using System.Reflection;

namespace RegionExtension.Database
{
    public class DatabaseTable<T>
    {
        public string Name { get; private set; }
        public IDbConnection Connection { get; private set; }

        private static Dictionary<Type, MySqlDbType> _types = new Dictionary<Type, MySqlDbType>()
            {
                { typeof(string), MySqlDbType.Text },
                { typeof(int), MySqlDbType.Int32 },
                { typeof(DateTime), MySqlDbType.Int64 }
            };

        public PropertyInfo[] PropertiesInfo { get; private set; }
        public PropertyInfo[] NonKeyProperties { get; private set; }
        public PropertyInfo KeyPropertie { get; private set; }

        public DatabaseTable(string name, IDbConnection connection)
        {
            Name = name;
            Connection = connection;
            PropertiesInfo = typeof(T).GetProperties();
            NonKeyProperties = PropertiesInfo.Where(IsSupportedNonKeyProperty).ToArray();
            KeyPropertie = PropertiesInfo.FirstOrDefault(IsPrimaryKeyProperty);
        }

        public IEnumerable<T> GetValues(Func<QueryResult, T> unitReader, params (string columnName, object value)[] conditions)
        {
            var res = new List<T>();
            var (whereClause, whereArgs) = BuildWhereClause(conditions);
            var query = $"SELECT * FROM {Name}{whereClause}";
            DbSafe.Execute($"Read rows from {Name}", () =>
            {
                using (var reader = Connection.QueryReader(query, whereArgs))
                {
                    while (reader.Read())
                        res.Add(unitReader(reader));
                }
            });
            return res;
        }

        public bool InitializeTable()
        {
            var properties = PropertiesInfo;
            var columns = new List<SqlColumn>();
            foreach (var property in properties)
            {
                MySqlDbType type;
                if (!_types.TryGetValue(property.PropertyType, out type))
                    continue;
                var column = new SqlColumn(property.Name, _types[property.PropertyType]);
                SetColumnProperties(property, column);
                columns.Add(column);
            }
            return DbSafe.Execute($"Ensure table {Name}", () =>
            {
                var creator = new SqlTableCreator(Connection, QueryBuilderFactory.Create(Connection));
                creator.EnsureTableStructure(new SqlTable(Name, columns.ToArray()));
                return true;
            });
        }

        private static void SetColumnProperties(PropertyInfo property, SqlColumn column)
        {
            var attribute = property.GetCustomAttributes(true).FirstOrDefault(a => a.GetType() == typeof(SQLSettingAttribute));
            if (attribute != null)
            {
                var settings = (SQLSettingAttribute)attribute;
                var columnProperties = column.GetType().GetProperties().Where(p => p.PropertyType == typeof(bool) &&
                                                                              settings.Settings.Any(s => s.ToString().Equals(p.Name, StringComparison.OrdinalIgnoreCase)));
                foreach (var prop in columnProperties)
                    prop.SetValue(column, true);
            }
        }

        private static bool IsPrimaryKeyProperty(PropertyInfo property) =>
            property.GetCustomAttributes(true).Any(a => a is SQLSettingAttribute settings &&
                                                        settings.Settings.Any(s => s == SqlColumnSettings.Primary));

        private static bool IsAutoIncrementProperty(PropertyInfo property) =>
            property.GetCustomAttributes(true).Any(a => a is SQLSettingAttribute settings &&
                                                        settings.Settings.Any(s => s == SqlColumnSettings.AutoIncrement));

        private static bool IsSupportedNonKeyProperty(PropertyInfo property) =>
            _types.ContainsKey(property.PropertyType) && !IsPrimaryKeyProperty(property) && !IsAutoIncrementProperty(property);

        public bool SaveValue(T dBUnit)
        {
            var properties = NonKeyProperties;
            var names = string.Join(", ", properties.Select(p => p.Name).ToArray());
            var placeholders = string.Join(", ", properties.Select((_, i) => $"@{i}"));
            var args = properties.Select(p => NormalizeDbValue(p.GetValue(dBUnit))).ToArray();
            return DbSafe.Execute($"Insert row into {Name}", () =>
            {
                Connection.Query($"INSERT INTO {Name} ({names}) VALUES ({placeholders});", args);
                return true;
            });
        }

        public bool RemoveByObject(T dBunit)
        {
            var keyProperty = KeyPropertie;
            if (keyProperty == null)
                throw new ArgumentException("Failed find primary key column!");
            return DbSafe.Execute($"Delete row from {Name} by object", () =>
            {
                Connection.Query($"DELETE FROM {Name} WHERE {keyProperty.Name}=@0", keyProperty.GetValue(dBunit) ?? DBNull.Value);
                return true;
            });
        }

        public bool RemoveByColumn(params (string columnName, object value)[] conditions)
        {
            var (whereClause, whereArgs) = BuildWhereClause(conditions);
            var query = $"DELETE FROM {Name}{whereClause}";
            return DbSafe.Execute($"Delete rows from {Name}", () =>
            {
                Connection.Query(query, whereArgs);
                return true;
            });
        }

        public bool UpdateByColumn(string columnName, object value, params (string columnName, object value)[] conditions)
        {
            var (whereClause, whereArgs) = BuildWhereClause(conditions, 1);
            var args = new object[] { NormalizeDbValue(value) }.Concat(whereArgs).ToArray();
            var query = $"UPDATE {Name} SET {columnName}=@0{whereClause}";
            return DbSafe.Execute($"Update rows in {Name}", () =>
            {
                Connection.Query(query, args);
                return true;
            });
        }

        private static (string whereClause, object[] args) BuildWhereClause((string columnName, object value)[] conditions, int startIndex = 0)
        {
            if (conditions == null || conditions.Length == 0)
                return (string.Empty, Array.Empty<object>());

            var parts = new List<string>(conditions.Length);
            var args = new object[conditions.Length];
            for (int i = 0; i < conditions.Length; i++)
            {
                parts.Add($"{conditions[i].columnName}=@{startIndex + i}");
                args[i] = NormalizeDbValue(conditions[i].value);
            }
            return (" WHERE " + string.Join(" AND ", parts), args);
        }

        private static object NormalizeDbValue(object value)
        {
            if (value is null)
                return DBNull.Value;
            if (value is DateTime dateTime)
                return DateTimeCodec.ToUnixMilliseconds(dateTime);
            return value;
        }
    }
}

