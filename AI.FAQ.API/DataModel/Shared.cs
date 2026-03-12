namespace AI.FAQ.API.DataModel
{
    public class BooleanResult
    {
        public bool Result { get; set; }
        public string? Message { get; set; }

        public object? Value { get; set; }

        public BooleanResult(bool result, string? message = null, object? value = null)
        {
            Result = result;
            Message = message;
            Value = value;
        }
    }
}
