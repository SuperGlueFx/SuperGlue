using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SuperGlue.Web.ModelBinding.ValueConverters
{
    public class ValueConverterCollection : IValueConverterCollection
    {
        private readonly IEnumerable<IValueConverter> _valueConverters;

        public ValueConverterCollection(IEnumerable<IValueConverter> valueConverters)
        {
            _valueConverters = valueConverters;
        }

        public IEnumerator<IValueConverter> GetEnumerator()
        {
            return _valueConverters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool CanConvert(Type destinationType)
        {
            return GetMatchingConverters(destinationType).Any();
        }

        public BindingResult Convert(Type destinationType, object value)
        {
            var converter = GetMatchingConverters(destinationType).FirstOrDefault();

            return converter == null ? new BindingResult(null, false) : converter.Convert(destinationType, value);
        }

        private IEnumerable<IValueConverter> GetMatchingConverters(Type destinationType)
        {
            return _valueConverters.Where(x => x.Matches(destinationType));
        } 
    }
}