import { ClassType, getMessageType, getMessageTypeConstructor } from "../contractMessage";
import { filter } from "rxjs";
import { MessageDelivery } from "./MessageDelivery";

export const filterByMessageType = <TMessage>(messageType: ClassType<TMessage>) =>
    filter((envelope: MessageDelivery): envelope is MessageDelivery<TMessage> =>
        getMessageTypeConstructor(messageType) === getMessageType(envelope.message));