using System;
using System.Collections.Generic;
using System.Text;
using SmartTraitsDefs;
using Pizza = System.Text;

namespace SmartTraits.Tests.Stage7
{
    [SimpleTrait]
    public class TNestedGeneric<X> where X : new()
    {
        public X GetX()
        {
            return new X();
        }
    }
}
