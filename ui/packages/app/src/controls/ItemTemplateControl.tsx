import { PropsWithChildren, useMemo } from "react";
import { getStackClassNames } from "./LayoutStackControl";
import { ControlView } from "../ControlDef";
import { useControlContext } from "../ControlContext";
import { LayoutStackSkin } from "@open-smc/layout/src/contract/controls/LayoutStackControl";

export interface ItemTemplateView extends ControlView {
    view: string;
    data?: unknown[];
    skin?: LayoutStackSkin;
}

export default function ItemTemplateControl({data, view, style, skin}: ItemTemplateView) {
    const parentContext = useDataContext();
    const {rawView} = useControlContext();

    const dataContext = useMemo(() =>
            new DataContext({items: rawView.data}, parentContext),
        [rawView, parentContext]
    );

    if (data?.length && !view) {
        throw 'View is missing';
    }

    const renderedItems =
        data?.map(
            (item, index) =>
                <ItemContext index={index} key={index} children={renderControl(view)}/>
        );

    const className = getStackClassNames(skin, false);

    return (
        <div className={className} style={style}>
            <DataContextProvider dataContext={dataContext}>
                {renderedItems}
            </DataContextProvider>
        </div>
    );
}

interface ItemContextProps {
    index: number;
}

function ItemContext({index, children}: PropsWithChildren & ItemContextProps) {
    const parentContext = useDataContext();

    const dataContext = useMemo(
        () => {
            return new DataContext({item: makeBinding(`items[${index}]`), index}, parentContext);
        },
        [index, parentContext]
    );

    return (
        <DataContextProvider dataContext={dataContext}>
            {children}
        </DataContextProvider>
    );
}

// example:
// const itemTemplateControl = {
//     dataContext: {
//         items: [1, 2, 3]
//     },
//     view: {
//         $type: "NumberControl",
//         data: new Binding("item")
//     },
//     data: new Binding("items")
// }