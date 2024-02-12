import { filter } from "rxjs";
import { MessageDelivery } from "../api/MessageDelivery";

export function filterByType<T>(ctor: new(...args: any[]) => T) {
    return filter<MessageDelivery>((envelope): envelope is MessageDelivery<T> =>
        envelope.message instanceof ctor);
}