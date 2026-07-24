using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    internal static class ZabbixDateTimeModelBuilderExtensions
    {
        private const string MinimumDateTimeText = "1970-01-01T00:00:01Z";

        private static readonly DateTime MinimumDateTime = DateTime.ParseExact(
            MinimumDateTimeText,
            "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal);

        private static readonly Expression<Func<DateTime, DateTime>> ToProvider =
            value => value <= MinimumDateTime
                ? MinimumDateTime
                : value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                    : value.ToUniversalTime();

        private static readonly Expression<Func<DateTime, DateTime>> FromProvider =
            value => value <= MinimumDateTime
                ? DateTime.MinValue
                : value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                    : value.ToUniversalTime();

        private static readonly Expression<Func<DateTime?, DateTime?>> NullableToProvider =
            value => value.HasValue
                ? value <= MinimumDateTime
                    ? MinimumDateTime
                    : value.Value.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                        : value.Value.ToUniversalTime()
                : null;

        private static readonly Expression<Func<DateTime?, DateTime?>> NullableFromProvider =
            value => value.HasValue
                ? value <= MinimumDateTime
                    ? DateTime.MinValue
                    : value.Value.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                        : value.Value.ToUniversalTime()
                : null;

        private static readonly ValueConverter<DateTime, DateTime> DateTimeConverter =
            new ValueConverter<DateTime, DateTime>(ToProvider, FromProvider);

        private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeConverter =
            new ValueConverter<DateTime?, DateTime?>(NullableToProvider, NullableFromProvider);

        public static PropertyBuilder<DateTime> HasSqlDateTimeAdjust(
            this PropertyBuilder<DateTime> source,
            bool onAdd = true,
            bool onUpdate = true)
        {
            var builder = source.HasConversion(DateTimeConverter);

            if (onAdd && onUpdate)
                builder = builder.ValueGeneratedOnAddOrUpdate();
            else if (onAdd)
                builder = builder.ValueGeneratedOnAdd();
            else if (onUpdate)
                builder = builder.ValueGeneratedOnUpdate();

            return builder;
        }

        public static PropertyBuilder<DateTime?> HasSqlDateTimeAdjust(
            this PropertyBuilder<DateTime?> source,
            bool onAdd = false,
            bool onUpdate = false)
        {
            var builder = source.HasConversion(NullableDateTimeConverter);

            if (onAdd && onUpdate)
                builder = builder.ValueGeneratedOnAddOrUpdate();
            else if (onAdd)
                builder = builder.ValueGeneratedOnAdd();
            else if (onUpdate)
                builder = builder.ValueGeneratedOnUpdate();

            return builder;
        }
    }
}
