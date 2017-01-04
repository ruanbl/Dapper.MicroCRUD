﻿// <copyright file="PropertySchema.cs" company="Berkeleybross">
// Copyright (c) Berkeleybross. All rights reserved.
// </copyright>
namespace Dapper.MicroCRUD.Schema
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Stores data useful for building a <see cref="ColumnSchema"/>.
    /// </summary>
    public class PropertySchema
    {
        /// <summary>
        /// Gets or sets the name of the property
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the original <see cref="PropertyInfo"/> used to create this builder.
        /// NB: To get an attribute efficiently, use the <see cref="FindAttribute{T}"/> method.
        /// </summary>
        public PropertyInfo PropertyInfo { get; set; }

        /// <summary>
        /// Gets or sets the custom attributes on the property.
        /// </summary>
        public object[] CustomAttributes { get; set; }

        /// <summary>
        /// Creates a <see cref="PropertySchema"/> from the <paramref name="property"/>.
        /// </summary>
        public static PropertySchema MakePropertySchema(PropertyInfo property)
        {
            return new PropertySchema
                {
                    CustomAttributes = property.GetCustomAttributes(false),
                    Name = property.Name,
                    PropertyInfo = property
                };
        }

        /// <summary>
        /// Gets the first attribute of type T or null
        /// </summary>
        public T FindAttribute<T>()
            where T : Attribute
        {
            foreach (var attribute in this.CustomAttributes)
            {
                var result = attribute as T;
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}