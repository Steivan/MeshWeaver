import { configureStore, createAction, createReducer, Dispatch } from "@reduxjs/toolkit"
import { dataSyncHub } from "./dataSyncHub";
import { Style } from "../contract/controls/Style";
import { createWorkspace } from "@open-smc/data/src/workspace";
import { subscribeToDataChanges } from "@open-smc/data/src/subscribeToDataChanges";
import { EntireWorkspace, LayoutAreaReference } from "@open-smc/data/src/data.contract";
import { MessageHub } from "@open-smc/message-hub/src/api/MessageHub";
import { fromLayoutArea } from "./fromLayoutArea";
import { distinctUntilChanged, distinctUntilKeyChanged, from, Observable, Subscription } from "rxjs";
import { LayoutArea } from "../contract/LayoutArea";

export type RootState = {
    rootArea: string;
    areas: Record<string, LayoutAreaModel>;
}

export type LayoutAreaModel = {
    id: string;
    control: ControlModel;
    options?: any;
    style?: Style;
}

export type ControlModel = {
    componentTypeName: string;
    props: { [prop: string]: unknown };
}

export interface SetPropAction {
    areaId: string;
    prop: string;
    value: any;
}

export const setProp = createAction<SetPropAction>('setProp');
export const setArea = createAction<LayoutAreaModel>('setArea');
export const removeArea = createAction<string>('removeArea');
export const setRoot = createAction<string>('setRoot');

const rootReducer = createReducer<RootState>(
    null,
    builder => {
        builder
            .addCase(setProp, (state, action) => {
                const {areaId, prop, value} = action.payload;
                (state.areas[areaId].control.props as any)[prop] = value;
            })
            .addCase(setArea, (state, action) => {
                const area = action.payload;
                state.areas[area.id] = area;
            })
            .addCase(removeArea, (state, action) => {
                delete state.areas[action.payload];
            })
            .addCase(setRoot, (state, action) => {
                state.rootArea = action.payload;
            })
    }
);

export const makeStore = (hub: MessageHub) => {
    const subscription = new Subscription();

    const dataStore = createWorkspace(undefined, "data");
    subscription.add(subscribeToDataChanges(hub, new EntireWorkspace(), dataStore.dispatch));

    const layoutStore = createWorkspace(undefined, "layout");

    const store = configureStore<RootState>({
        preloadedState: {
            rootArea: null,
            areas: {}
        },
        reducer: rootReducer,
        devTools: {
            name: "ui"
        }
    });

    subscription.add(
        fromLayoutArea(layoutStore, [], store)
            .pipe(distinctUntilKeyChanged("id"))
            .subscribe(layoutAreaModel => {
                store.dispatch(setRoot(layoutAreaModel.id));
            })
    );

    subscription.add(subscribeToDataChanges(hub, new LayoutAreaReference("/"), layoutStore.dispatch));

    return store;
}

export const store = makeStore(dataSyncHub);

export type AppStore = typeof store;

export type AppDispatch = AppStore["dispatch"];

export const layoutAreaSelector = (id: string) => (state: RootState) => state.areas[id];