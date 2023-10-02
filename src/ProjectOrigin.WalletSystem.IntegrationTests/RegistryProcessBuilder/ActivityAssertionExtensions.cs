using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using MassTransit;
using MassTransit.Courier.Contracts;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public static class ActivityAssertionExtensions
{
    public static bool IsEqual(this Electricity.V1.Commitment a, SecretCommitmentInfo b)
    {
        return a.Content.Span.SequenceEqual(b.Commitment.C);
    }

    public static void ShouldSetStates(this Activity activity, Dictionary<Guid, ReceivedSliceState> states)
    {
        activity.Name.Should().Be(nameof(UpdateSliceStateActivity));
        var statesArg = activity.Arguments["sliceStates"].Should().BeAssignableTo<IDictionary<Guid, ReceivedSliceState>>().Which;
        statesArg.Should().BeEquivalentTo(states);
    }

    public static void ShouldWaitFor(this Activity activity, Transaction transaction)
    {
        var argument = activity.ShouldBeActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>();

        argument.RegistryName.Should().Be(transaction.Header.FederatedStreamId.Registry);
        argument.TransactionId.Should().BeEquivalentTo(transaction.ToShaId());
    }

    public static TArgument ShouldBeActivity<TActivity, TArgument>(this Activity activity) where TActivity : IExecuteActivity<TArgument> where TArgument : class
    {
        activity.Name.Should().Be(typeof(TActivity).Name);
        return Populate<TArgument>(activity.Arguments);
    }

    public static T Populate<T>(this IDictionary<string, object> dictionary)
    {
        var obj = Activator.CreateInstance<T>();
        foreach (var kvp in dictionary)
        {
            string keyUpper = kvp.Key.ToUpperFirstChar();
            var property = typeof(T).GetProperty(kvp.Key) ?? typeof(T).GetProperty(keyUpper);
            if (property != null && property.CanWrite)
            {
                var value = Convert.ChangeType(kvp.Value, property.PropertyType);
                property.SetValue(obj, value);
            }
        }
        return obj;
    }

    public static (Transaction, TEvent) ShouldBeTransactionWithEvent<TEvent>(
        this Activity activity,
        System.Linq.Expressions.Expression<Func<Transaction, bool>> transactionPredicate,
        System.Linq.Expressions.Expression<Func<TEvent, bool>> payloadPredicate
        ) where TEvent : IMessage<TEvent>
    {
        var descritor = typeof(TEvent).GetProperty(nameof(IMessage.Descriptor))?.GetValue(null, null) as MessageDescriptor
            ?? throw new InvalidOperationException("Descriptor not found");

        var argument = ShouldBeActivity<SendRegistryTransactionActivity, SendRegistryTransactionArguments>(activity);
        argument.Transaction.Should().Match(transactionPredicate);
        var payload = (TEvent)descritor!.Parser.ParseFrom(argument.Transaction.Payload);

        payload.Should().Match(payloadPredicate);

        return (argument.Transaction, payload);
    }

    private static string ToUpperFirstChar(this string value)
    {
        var keyFirstUpper = new Span<char>(new char[1]);
        value.AsSpan(0, 1).ToUpperInvariant(keyFirstUpper);
        return string.Concat(keyFirstUpper, value.AsSpan(1));
    }
}
