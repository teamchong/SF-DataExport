<v-modal @close="dispatch('showOrgModal',false)">
    <template #header>
        <h2 id="modal-heading-01" class="slds-text-heading_medium slds-hyphenate">Manage Organizations</h2>
    </template>
    <div v-if="orgSettings.length">
        <h3>Saved Organizations</h3>
        <ul>
            <li style="margin:2em;" v-for="org in orgSettings">
                <div class="slds-box">
                    <button class="slds-button slds-button_neutral slds-button_stretch" style="padding:1em;font-size:150%;text-align:left;margin-bottom:0.2em" @click="dispatch('attemptLogin',org)">
                        <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                        {{org|orgname}}
                    </button>

                    <button class="slds-button slds-button_neutral" @click="dispatch('removeOrg',org)">
                        <button-iconleft type="action" icon="remove"></button-iconleft>
                        Revoke Access
                    </button>

                    <button class="slds-button slds-button_neutral" v-if="orgHasOfflineAccess(org)" @click="dispatch('removeOfflineAccess',org)">
                        <button-iconleft type="action" icon="remove"></button-iconleft>
                        Remove Offline Access
                    </button>
                </div>
            </li>
        </ul>
    </div>
    <h3>New Organization</h3>
    <ul>
        <li style="margin:2em;">
            <div class="slds-box">
                <button class="slds-button slds-button_neutral" style="width:100%;padding:1em;font-size:150%;text-align:left" @click="dispatch('attemptLogin','login.salesforce.com')">
                    <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                    Production (login.salesforce.com)
                </button>
            </div>
        </li>
        <li style="margin:2em;">
            <div class="slds-box">
                <button class="slds-button slds-button_neutral" style="width:100%;padding:1em;font-size:150%;text-align:left" @click="dispatch('attemptLogin','test.salesforce.com')">
                    <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                    Sandbox (test.salesforce.com)
                </button>
            </div>
        </li>
        <!--<li style="margin:2em;">
            <div class="slds-box">
                <button class="slds-button slds-button_neutral" style="width:100%;padding:1em;font-size:150%;text-align:left" @click="dispatch('attemptLogin',customLoginUrl)" :disabled="!customLoginUrl">
                    <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                    Custom ({{customLoginUrlHint}})
                </button>
                <div class="slds-form-element">
                    <div class="slds-form-element__control">
                        <input placeholder="*.my.salesforce.com" class="slds-input" type="text" v-model="customLoginUrlInput" />
                    </div>
                </div>
            </div>
        </li>-->
    </ul>
    <hr />
    <div class="slds-form-element">
        <div class="slds-clearfix">
            <div class="slds-float_right">
                ss
                <button class="slds-button slds-button_success" @click="dispatch('setOrgSettingsPath',orgSettingsPath)">PaSave pathtt</button>
            </div>
            <label class="slds-form-element__label" for="text-input-id-1">Org settings file path</label>
        </div>
        <div class="slds-form-element__control" style="margin-top:0.1em;">
            <v-autocomplete v-model="orgSettingsPath" :items="orgSettingsPathItems" solo dense
                            id="text-input-id-1" :search-input.sync="fetchOrgSettingsPath"></v-autocomplete>
        </div>
    </div>
</v-modal>