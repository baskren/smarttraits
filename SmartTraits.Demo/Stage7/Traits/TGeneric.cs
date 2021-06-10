using System;
using System.Collections.Generic;
using System.Text;
using SmartTraitsDefs;

namespace SmartTraits.Tests.Stage7
{
    [SimpleTrait]
    public partial class TGeneric<T, U> where U: IName, new()
    {
        public T Item { get; set; }

        public U Cheese { get; set; }

        public TGeneric (T item, U cheese)
        {
            Item = item;
            Cheese = cheese;
        }

    }
}
