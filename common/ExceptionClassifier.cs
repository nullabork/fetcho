using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho.Common
{
    public static class ExceptionClassifier
    {
        public static ExceptionClassification Classify(string exception)
        {
            if (String.IsNullOrWhiteSpace(exception)) return ExceptionClassification.Unsure;
            if (exception.Contains("(300)")) return ExceptionClassification.HTTP300;
            else if (exception.Contains("(302)")) return ExceptionClassification.HTTP302;
            else if (exception.Contains("(308)")) return ExceptionClassification.HTTP308;
            else if (exception.Contains("(400)")) return ExceptionClassification.HTTP400;
            else if (exception.Contains("(401)")) return ExceptionClassification.HTTP401;
            else if (exception.Contains("(403)")) return ExceptionClassification.HTTP403;
            else if (exception.Contains("(404)")) return ExceptionClassification.HTTP404;
            else if (exception.Contains("(405)")) return ExceptionClassification.HTTP405;
            else if (exception.Contains("(406)")) return ExceptionClassification.HTTP406;
            else if (exception.Contains("(410)")) return ExceptionClassification.HTTP410;
            else if (exception.Contains("(429)")) return ExceptionClassification.HTTP429;
            else if (exception.Contains("(496)")) return ExceptionClassification.HTTP496;
            else if (exception.Contains("(500)")) return ExceptionClassification.HTTP500;
            else if (exception.Contains("(502)")) return ExceptionClassification.HTTP502;
            else if (exception.Contains("(503)")) return ExceptionClassification.HTTP503;
            else if (exception.Contains("(520)")) return ExceptionClassification.HTTP520;
            else if (exception.Contains("(999)")) return ExceptionClassification.HTTP999;
            else if (exception.StartsWith("System.TimeoutException", StringComparison.InvariantCulture)) return ExceptionClassification.Timeout;
            else if (exception.Contains(" blocked,")) return ExceptionClassification.Blocked;
            else if (exception.Contains("The remote name could not be resolved:")) return ExceptionClassification.DNSResolve;
            else if (exception.Contains("established connection failed because connected host has failed to respond") ||
                     exception.Contains("No connection could be made because the target machine actively refused it"))
                return ExceptionClassification.Connection;
            else return ExceptionClassification.Unsure;

        }
    }

    public enum ExceptionClassification
    {
        NotClassified,
        HTTP300,
        HTTP302,
        HTTP308,
        HTTP400,
        HTTP401,
        HTTP402,
        HTTP403,
        HTTP404,
        HTTP405,
        HTTP406,
        HTTP407,
        HTTP408,
        HTTP409,
        HTTP410,
        HTTP429,
        HTTP496,
        HTTP500,
        HTTP502,
        HTTP503,
        HTTP520,
        HTTP999,
        Connection,
        DNSResolve,
        Timeout,
        Blocked,
        Unsure
    }
}
