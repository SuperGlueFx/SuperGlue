using System;
using System.Collections.Generic;

namespace Jajo.Web.ModelBinding
{
    public interface IValueConverterCollection : IEnumerable<IValueConverter>
    {
        bool CanConvert(Type destinationType);
        object Convert(Type destinationType, object value);
    }
}