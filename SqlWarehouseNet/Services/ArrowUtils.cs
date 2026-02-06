using Apache.Arrow;

namespace SqlWarehouseNet.Services;

/// <summary>
/// Utility to extract typed values from Apache Arrow columns without reflection.
/// Supports all common Databricks SQL types for AOT compatibility.
/// </summary>
public static class ArrowUtils
{
    public static object? GetValueFromArrowColumn(IArrowArray column, int index)
    {
        return column switch
        {
            StringArray stringArray => stringArray.GetString(index),
            Int8Array int8Array => int8Array.GetValue(index),
            Int16Array int16Array => int16Array.GetValue(index),
            Int32Array int32Array => int32Array.GetValue(index),
            Int64Array int64Array => int64Array.GetValue(index),
            UInt8Array uint8Array => uint8Array.GetValue(index),
            UInt16Array uint16Array => uint16Array.GetValue(index),
            UInt32Array uint32Array => uint32Array.GetValue(index),
            UInt64Array uint64Array => uint64Array.GetValue(index),
            FloatArray floatArray => floatArray.GetValue(index),
            DoubleArray doubleArray => doubleArray.GetValue(index),
            Decimal128Array decimal128Array => decimal128Array.GetValue(index),
            Decimal256Array decimal256Array => decimal256Array.GetValue(index),
            BooleanArray boolArray => boolArray.GetValue(index),
            Date32Array date32Array => date32Array.GetDateTime(index),
            Date64Array date64Array => date64Array.GetDateTime(index),
            TimestampArray timestampArray => timestampArray.GetTimestamp(index),
            Time32Array time32Array => time32Array.GetValue(index),
            Time64Array time64Array => time64Array.GetValue(index),
            BinaryArray binaryArray => binaryArray.GetBytes(index).ToArray(),
            _ => $"[unsupported:{column.GetType().Name}]",
        };
    }
}
