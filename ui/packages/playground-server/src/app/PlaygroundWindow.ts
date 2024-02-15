import { AreaChangedEvent, ClickedEvent } from "@open-smc/application/src/contract/application.contract";
import { MainWindowStack, makeStack } from "@open-smc/sandbox/src/LayoutStack";
import { makeMenuItem } from "@open-smc/sandbox/src/MenuItem";
import { uiAddress } from "../contract";
import fs from "fs";
import { mainWindowAreas } from "@open-smc/application/src/controls/mainWindowApi.ts";

const rootDir = "./notebooks";

export class PlaygroundWindow extends MainWindowStack {
    constructor() {
        super();

        this.withAddress("PlaygroundWindow");
        this.withSideMenu(this.makeSideMenu());
    }

    private makeSideMenu() {
        const sideMenu = makeStack()
            .withAddress("SideMenu");

        const files = fs.readdirSync(rootDir, {withFileTypes: true});

        for (const file of files) {
            sideMenu.withView(
                makeMenuItem()
                    .withTitle(file.name)
                    .withClickMessage()
                    .withMessageHandler(ClickedEvent, () => {
                        this.openNotebook(file);
                    })
            );
        }

        return sideMenu;
    }

    private openNotebook(file: fs.Dirent) {
        this.sendMessage(
            new AreaChangedEvent(
                mainWindowAreas.main,
                makeMenuItem()
                    .withTitle(`Notebook stub ${file.name}`)
            ),
            uiAddress
        );
    }
}