using System;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Exceptions
{
    public class MigrationException : Exception
    {
        public  MigrationException(string message) : base(message) { }
        public MigrationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
