using System.Collections.Generic;

namespace Catnap.Common
{
    public class DbCommandSpec
    {
        private string commandText;
        private List<Parameter> parameters = new List<Parameter>();

        public DbCommandSpec SetCommandText(string value)
        {
            commandText = value;
            return this;
        }

        public IEnumerable<Parameter> Parameters
        {
            get { return parameters; }
        }

        public DbCommandSpec AddParameter(object value)
        {
            return AddParameter(null, value);
        }

        public DbCommandSpec AddParameter(string name, object value)
        {
            parameters.Add(new Parameter(name, value));
            return this;
        }

        public override string ToString()
        {
            return commandText;
        }
    }
}