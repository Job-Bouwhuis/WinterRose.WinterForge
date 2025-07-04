using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal class IConvertableToIConvertable<TTarget>
        : TypeConverter<IConvertible, IConvertible> where TTarget : IConvertible
    {
        public override IConvertible Convert(IConvertible source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Use ChangeType to convert source to the target type
            object converted = System.Convert.ChangeType(source, typeof(TTarget));
            return (IConvertible)converted;
        }
    }
}
