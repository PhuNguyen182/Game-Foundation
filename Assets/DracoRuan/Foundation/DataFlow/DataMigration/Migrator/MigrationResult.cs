using System;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Migrator
{
    public readonly struct MigrationResult
    {
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        public MigrationResult(bool isSuccess, string errorMessage = null, Exception exception = null)
        {
            this.IsSuccess = isSuccess;
            this.ErrorMessage = errorMessage;
            this.Exception = exception;
        }

        public MigrationResult Succeeded() => new(true);

        public MigrationResult Failed(string errorMessage = null, Exception exception = null) =>
            new(false, errorMessage, exception);

        public override string ToString()
        {
            if (this.IsSuccess)
                return "Migration Succeeded";
            
            string exception = this.Exception?.Message;
            string message = string.IsNullOrEmpty(exception)
                ? $"Migration failed with message: {this.ErrorMessage}"
                : $"Migration failed with message: {this.ErrorMessage}. More information: {exception}";
            return message;
        }
    }
}
