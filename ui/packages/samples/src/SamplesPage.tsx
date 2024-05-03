import { useEffect, useState } from "react";
import { Provider } from "react-redux";
import { appStore } from "@open-smc/app/src/store/appStore.ts";
import App from "@open-smc/app/src/App.tsx";
import { renderLayoutAreaReference } from "@open-smc/app/src/store/renderLayoutAreaReference.ts";
import { useLocation } from "react-router-dom";
import { HmrClientHub } from "./HmrClientHub.tsx";
import { connectHubs } from "@open-smc/messaging/src/middleware/connectHubs.ts";
import { UiHub } from "@open-smc/app/src/UiHub.ts";
import { LayoutAreaReference } from "@open-smc/data/src/contract/LayoutAreaReference.ts";
import { SerializationMiddleware } from "@open-smc/middleware/src/SerializationMiddleware.ts";

export function SamplesPage() {
    const {pathname} = useLocation();
    const [hmrClientHub] = useState(new HmrClientHub());
    const [uiHub] = useState(new UiHub());

    useEffect(() => {
        const subscription = connectHubs(uiHub, new SerializationMiddleware(hmrClientHub))
        return () => subscription.unsubscribe();
    }, [uiHub, hmrClientHub]);

    useEffect(
        () => renderLayoutAreaReference(uiHub, new LayoutAreaReference("/main")),
        [uiHub, pathname]
    );

    return (
        <Provider store={appStore}>
            <App/>
        </Provider>
    );
}