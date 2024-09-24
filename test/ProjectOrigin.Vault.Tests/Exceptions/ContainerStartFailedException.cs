namespace ProjectOrigin.Vault.Tests.Exceptions
{
    using System;

    /// <summary>
    /// Exception that is thrown when a container failed to start.
    /// </summary>
    public class ContainerStartFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerStartFailedException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ContainerStartFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerStartFailedException"/> class.
        /// </summary>
        /// <param name="stdout">The standard output of the container.</param>
        /// <param name="stderr">The standard error of the container.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ContainerStartFailedException(string stdout, string stderr, Exception innerException)
            : base($"Container failed to start. Logs:\nStdout: {stdout}\nStderr:{stderr}\n", innerException)
        {
        }
    }
}
