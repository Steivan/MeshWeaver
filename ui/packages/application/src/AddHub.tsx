import { createContext, PropsWithChildren, useContext, useEffect, useState } from "react";
import { ConnectToHubRequest } from "./contract/application.contract";
import { useMessageRouter } from "./MessageRouter";
import { MessageHub } from "@open-smc/message-hub/src/api/MessageHub";
import { makeProxy } from "@open-smc/message-hub/src/middleware/makeProxy";
import { sendMessage } from "@open-smc/message-hub/src/sendMessage";

const hubContext = createContext<MessageHubFinder>(null);

type MessageHubFinder = (id: string) => MessageHub;

export function useMessageHub(id?: string) {
    return useContext(hubContext)?.(id);
}

interface MessageHubProps {
    address: any;
    id?: string;
}

export function AddHub({address, id, children}: PropsWithChildren<MessageHubProps>) {
    const parentContextValue = useContext(hubContext);
    const {addHub, uiAddress} = useMessageRouter();
    const [value, setValue] = useState<MessageHubFinder>();

    useEffect(() => {
        const [hub, exposed] = makeProxy();
        const subscription = addHub(address, exposed);
        sendMessage(hub, new ConnectToHubRequest(uiAddress, address));
        setValue(() =>
            (hubId: string) => {
                if (!hubId || hubId === id) {
                    return hub;
                }
                return parentContextValue?.(hubId);
            })
        return () => subscription.unsubscribe();
    }, [address, id, addHub]);

    if (!value) {
        return null;
    }

    return (
        <hubContext.Provider value={value} children={children}/>
    );
}