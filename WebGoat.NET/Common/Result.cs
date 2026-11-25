using System;


namespace WebGoatCore.Common // eller WebGoatCore.Models
{
    /// <summary>
    /// Generisk Result Object (Secure by Design, kapitel 9).
    /// Adskiller forretningsafvisninger fra tekniske undtagelser uden at kaste tunge exceptions.
    /// </summary>
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public T Value { get; }
        public string ErrorMessage { get; }

        protected Result(T value, bool isSuccess, string errorMessage)
        {
            Value = value;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static Result<T> Success(T value) => new Result<T>(value, true, string.Empty);
        public static Result<T> Failure(string errorMessage) => new Result<T>(default, false, errorMessage);
    }
}
