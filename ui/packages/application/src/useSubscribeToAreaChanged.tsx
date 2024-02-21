import { AreaChangedEvent } from "./contract/application.contract";
import { useEffect } from "react";
import { ofContractType } from "./contract/ofContractType";
import { filter, map, Observable } from "rxjs";
import { MessageDelivery } from "@open-smc/message-hub/src/api/MessageDelivery";

export function useSubscribeToAreaChanged<TObservable extends Observable<MessageDelivery>>(
    hub: TObservable,
    area: string,
    handler: (message: AreaChangedEvent) => void
) {
    useEffect(() => {
        const subscription =
            hub.pipe(ofContractType(AreaChangedEvent))
                .pipe(filter(({message}) => area === undefined || message.area === area))
                .pipe(map(({message}) => message))
                .subscribe(handler);
        return () => subscription.unsubscribe();
    }, [hub, area, handler]);
}