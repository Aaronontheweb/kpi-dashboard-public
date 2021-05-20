// -----------------------------------------------------------------------
// <copyright file="ApiQueryResult.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Util;

namespace Petabridge.KPI
{
    public sealed class ApiQueryResult
    {
        public ApiQueryResult(string operationName, int statusCode, string message)
        {
            OperationName = operationName;
            StatusCode = statusCode;
            Message = message;
        }

        public string OperationName { get; }
        public int StatusCode { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"{OperationName}(Status={StatusCode}, Message={Message})";
        }

        public ApiQueryResult With(string operationName = null, int? statusCode = null, string message = null)
        {
            return new ApiQueryResult(operationName ?? OperationName, statusCode ?? StatusCode, message ?? Message);
        }

        public static readonly ApiQueryResult Empty = new ApiQueryResult(string.Empty, 404, string.Empty);

        public static readonly ApiQueryResult Cancelled =
            Empty.With(statusCode: 499, message: "client cancelled request");

        public static readonly ApiQueryResult WriteSuccess = Empty.With(statusCode: 201, message: "write success");

        public static readonly ApiQueryResult WriteFailure =
            Empty.With(statusCode: 500, message: "write processing failure");
    }
}