import { useEffect, useState } from "react";
import { AreaChangedEvent, SetAreaRequest } from "./application.contract";
import { useSubscribeToAreaChanged } from "./useSubscribeToAreaChanged";
import { renderControl } from "./renderControl";
import { useApp } from "./App";

interface ControlStarterProps {
    area: string;
    path: string;
    options?: unknown
}

export function ControlStarter({area, path, options}: ControlStarterProps) {
    const [event, setEvent] = useState<AreaChangedEvent>();
    const {messageHub} = useApp();
    const {sendMessage} = messageHub;

    useSubscribeToAreaChanged(setEvent, area, messageHub);

    useEffect(() => {
        sendMessage(new SetAreaRequest(area, path, options));
        return () => sendMessage(new SetAreaRequest(area, null));
    }, [area, path, options, sendMessage]);

    if (!event?.view) {
        return null;
    }

    return renderControl(event?.view);
}