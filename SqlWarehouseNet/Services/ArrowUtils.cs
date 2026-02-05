using Apache.Arrow;

namespace SqlWarehouseNet.Services;

public static class ArrowUtils
{
    public static object? GetValueFromArrowColumn(IArrowArray column, int index)
    {
        if (column is StringArray stringArray) return stringArray.GetString(index);
        if (column is Int32Array int32Array) return int32Array.GetValue(index);
        if (column is Int64Array int64Array) return int64Array.GetValue(index);
        if (column is DoubleArray doubleArray) return doubleArray.GetValue(index);
        if (column is FloatArray floatArray) return floatArray.GetValue(index);
        if (column is BooleanArray boolArray) return boolArray.GetValue(index);
        if (column is TimestampArray timestampArray) return timestampArray.GetTimestamp(index);
        if (column is Date32Array date32Array) return date32Array.GetDateTime(index);
        
        return column.GetType().GetMethod("GetValue")?.Invoke(column, new object[] { index });
    }
}
