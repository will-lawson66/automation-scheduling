using CsvHelper.TypeConversion;

/// <summary>
/// A column specification for a CMR File Column
/// </summary>
public class CmrColumnSpecification
{
    public string PropertyName { get; set; }
    public string ColumnName { get; set; }
    public Type PropertyType { get; set; }
    public bool IsOptional { get; set; }
    public object DefaultValue { get; set; }
    public ITypeConverter TypeConverter { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="propertyName">Property Name for the Class Map</param>
    /// <param name="columnName">Column Name</param>
    /// <param name="propertyType">type of the property</param>
    public CmrColumnSpecification(string propertyName, string columnName, Type propertyType)
    {
        PropertyName = propertyName;
        ColumnName = columnName;
        PropertyType = propertyType;
        IsOptional = false;
        DefaultValue = null;
        TypeConverter = null;
    }

    public CmrColumnSpecification SetOptional(bool isOptional = true)
    {
        IsOptional = isOptional;
        return this;
    }

    public CmrColumnSpecification SetDefaultValue(object defaultValue)
    {
        DefaultValue = defaultValue;
        return this;
    }

    public CmrColumnSpecification SetTypeConverter(ITypeConverter typeConverter)
    {
        TypeConverter = typeConverter;
        return this;
    }
}
