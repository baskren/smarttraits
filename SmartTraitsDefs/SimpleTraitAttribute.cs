using System;

namespace SmartTraitsDefs
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class SimpleTraitAttribute : System.Attribute
    {
        public TraitOptions Options { get; set; }

        public SimpleTraitAttribute(TraitOptions options = TraitOptions.Normal)
        {
            Options = options;
        }
    }
}
