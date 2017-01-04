// <copyright file="TableSchemaFactory.cs" company="Berkeleybross">
// Copyright (c) Berkeleybross. All rights reserved.
// </copyright>
namespace Dapper.MicroCRUD.Schema
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using Dapper.MicroCRUD.Utils;

    /// <summary>
    /// Methods to create an instance of a <see cref="TableSchema"/>.
    /// </summary>
    internal class TableSchemaFactory
    {
        private static readonly object LockObject = new object();
        private static readonly ConcurrentDictionary<TableSchemaCacheIdentity, TableSchema> Schemas =
            new ConcurrentDictionary<TableSchemaCacheIdentity, TableSchema>();

        private static readonly List<Type> PossiblePropertyTypes = new List<Type>
            {
                typeof(byte),
                typeof(sbyte),
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(float),
                typeof(double),
                typeof(decimal),
                typeof(bool),
                typeof(string),
                typeof(char),
                typeof(Guid),
                typeof(DateTime),
                typeof(DateTimeOffset),
                typeof(byte[])
            };

        private static TableSchemaFactory current = new TableSchemaFactory(
            new DefaultTableNameFactory(),
            new DefaultColumnNameFactory());

        private readonly ITableNameFactory tableNameFactory;
        private readonly IColumnNameFactory columnNameFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableSchemaFactory"/> class.
        /// </summary>
        public TableSchemaFactory(
            ITableNameFactory tableNameFactory,
            IColumnNameFactory columnNameFactory)
        {
            this.tableNameFactory = tableNameFactory;
            this.columnNameFactory = columnNameFactory;
        }

        /// <summary>
        /// Gets the current table schema factory
        /// </summary>
        public static TableSchemaFactory Current
        {
            get
            {
                lock (LockObject)
                {
                    return current;
                }
            }
        }

        /// <summary>
        /// Updates the current factory by setting it to the result of the <paramref name="updater"/>.
        /// </summary>
        public static void SetCurrent(Func<TableSchemaFactory, TableSchemaFactory> updater)
        {
            lock (LockObject)
            {
                var result = updater(current);
                if (result != null)
                {
                    current = result;
                }
                else
                {
                    throw new ArgumentException("Updater returned null");
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="TableSchema"/> for the specified entityType and dialect.
        /// </summary>
        public static TableSchema GetTableSchema(Type entityType, Dialect dialect)
        {
            dialect = dialect ?? MicroCRUDConfig.DefaultDialect;

            var key = new TableSchemaCacheIdentity(entityType, dialect.Name);

            TableSchema result;
            if (Schemas.TryGetValue(key, out result))
            {
                return result;
            }

            var schema = Current.MakeTableSchema(entityType, dialect);
            Schemas[key] = schema;
            return schema;
        }

        /// <summary>
        /// Create the table schema for an entity
        /// </summary>
        public TableSchema MakeTableSchema(Type entityType, Dialect dialect)
        {
            var tableName = this.tableNameFactory.GetTableName(entityType, dialect);
            var properties = entityType.GetProperties()
                                       .Where(p =>
                                       {
                                           var propertyType = p.PropertyType.GetUnderlyingType();
                                           return propertyType.IsEnum || PossiblePropertyTypes.Contains(propertyType);
                                       })
                                       .Select(PropertySchema.MakePropertySchema)
                                       .Where(p => p.FindAttribute<NotMappedAttribute>() == null)
                                       .ToList();

            var explicitKeyDefined = properties.Any(p => p.FindAttribute<KeyAttribute>() != null);

            var columns = properties.Select(p => this.MakeColumnSchema(dialect, p, GetColumnUsage(explicitKeyDefined, p)));

            return new TableSchema(tableName, columns.ToImmutableArray());
        }

        /// <summary>
        /// Creates a new <see cref="TableSchemaFactory"/> which generates table names with the <paramref name="factory"/>.
        /// </summary>
        public TableSchemaFactory WithTableNameFactory(ITableNameFactory factory)
        {
            return new TableSchemaFactory(factory, this.columnNameFactory);
        }

        /// <summary>
        /// Creates a new <see cref="TableSchemaFactory"/> which generates column names with the <paramref name="factory"/>.
        /// </summary>
        public TableSchemaFactory WithColumnNameFactory(IColumnNameFactory factory)
        {
            return new TableSchemaFactory(this.tableNameFactory, factory);
        }

        private static ColumnUsage GetColumnUsage(bool explicitKeyDefined, PropertySchema property)
        {
            var isPrimaryKey = explicitKeyDefined
                ? property.FindAttribute<KeyAttribute>() != null
                : string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase);

            if (!property.PropertyInfo.CanWrite)
            {
                return isPrimaryKey
                    ? ColumnUsage.ComputedPrimaryKey
                    : ColumnUsage.ComputedColumn;
            }

            var generatedAttribute = property.FindAttribute<DatabaseGeneratedAttribute>();
            return isPrimaryKey
                ? GetPrimaryKeyUsage(generatedAttribute)
                : GetColumnUsage(generatedAttribute);
        }

        private static ColumnUsage GetColumnUsage(DatabaseGeneratedAttribute generatedAttribute)
        {
            if (generatedAttribute == null)
            {
                return ColumnUsage.Column;
            }

            switch (generatedAttribute.DatabaseGeneratedOption)
            {
                case DatabaseGeneratedOption.None:
                    return ColumnUsage.Column;
                case DatabaseGeneratedOption.Identity:
                    return ColumnUsage.GeneratedColumn;
                case DatabaseGeneratedOption.Computed:
                    return ColumnUsage.ComputedColumn;
                default:
                    throw new ArgumentOutOfRangeException(
                        "Unknown DatabaseGeneratedOption: " + generatedAttribute.DatabaseGeneratedOption);
            }
        }

        private static ColumnUsage GetPrimaryKeyUsage(DatabaseGeneratedAttribute generatedAttribute)
        {
            if (generatedAttribute == null)
            {
                return ColumnUsage.ComputedPrimaryKey;
            }

            switch (generatedAttribute.DatabaseGeneratedOption)
            {
                case DatabaseGeneratedOption.None:
                    return ColumnUsage.NotGeneratedPrimaryKey;
                case DatabaseGeneratedOption.Identity:
                case DatabaseGeneratedOption.Computed:
                    return ColumnUsage.ComputedPrimaryKey;
                default:
                    throw new ArgumentOutOfRangeException(
                        "Unknown DatabaseGeneratedOption: " + generatedAttribute.DatabaseGeneratedOption);
            }
        }

        private ColumnSchema MakeColumnSchema(Dialect dialect, PropertySchema property, ColumnUsage columnUsage)
        {
            var propertyName = property.Name;

            return new ColumnSchema(
                dialect.EscapeMostReservedCharacters(this.columnNameFactory.GetColumnName(property)),
                dialect.EscapeMostReservedCharacters(propertyName),
                propertyName,
                columnUsage);
        }
    }
}