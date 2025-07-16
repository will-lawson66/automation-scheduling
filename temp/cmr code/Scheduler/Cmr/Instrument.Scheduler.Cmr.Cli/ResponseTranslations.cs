namespace Instrument.Scheduler.Cmr.Cli;

using Instrument.Cli.Helpers;
using Instrument.Cli;
using Instrument.Scheduler.Cmr.Grpc.Message;

internal static class ResponseTranslations
{
    /// <summary>
    /// Translates a <see cref="PrepareCmrFileResponse"/> into an <see cref="ICommandResult"/>.
    /// </summary>
    /// <param name="response">The response to convert.</param>
    /// The CLI completed message.
    /// </returns>
    public static ICommandResult ToCliResult(this PrepareCmrFileResponse _)
    {
        return CommandHelper.CreateCompleteResult();
    }

    /// <summary>
    /// Translates a <see cref="LoadCmrFileResponse"/> into an <see cref="ICommandResult"/>.
    /// </summary>
    /// <param name="response">The response to convert.</param>
    /// The CLI completed message.
    /// </returns>
    public static ICommandResult ToCliResult(this LoadCmrFileResponse _)
    {
        return CommandHelper.CreateCompleteResult();
    }

    /// <summary>
    /// Translates a <see cref="ExecuteCmrFileResponse"/> into an <see cref="ICommandResult"/>.
    /// </summary>
    /// <param name="response">The response to convert.</param>
    /// The CLI completed message.
    /// </returns>
    public static ICommandResult ToCliResult(this ExecuteCmrFileResponse _)
    {
        return CommandHelper.CreateCompleteResult();
    }


}
