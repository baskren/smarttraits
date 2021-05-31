using System;

namespace SmartTraitsDefs
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class AddSimpleTraitAttribute : System.Attribute
    {
        private Type _traitType;

        public AddSimpleTraitAttribute(Type traitType)
        {
            _traitType = traitType;
        }
    }
}
