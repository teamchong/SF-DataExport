<v-modal @close="dispatch('showOrgModal',false)">
    <template #header>
        <h2 class="slds-text-heading_medium slds-hyphenate">Manage Organizations</h2>
    </template>
    <div v-if="orgSettings.length">
        <h3>Saved Organizations</h3>
        <ul>
            <li style="margin:2em;" v-for="org in orgSettings">
                <div class="slds-box">
                    <button class="slds-button slds-button_neutral slds-button_stretch" style="padding:1em;font-size:150%;text-align:left;margin-bottom:0.2em" @click="dispatch('AttemptLogin',org)">
                        <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                        {{org|orglabel}}
                    </button>

                    <button class="slds-button slds-button_neutral" @click="dispatch('RemoveOrg',org)">
                        <button-iconleft type="action" icon="remove"></button-iconleft>
                        Revoke Access
                    </button>

                    <button class="slds-button slds-button_neutral" v-if="orgHasOfflineAccess(org)" @click="dispatch('RemoveOfflineAccess',org)">
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
                <button class="slds-button slds-button_neutral" style="width:100%;padding:1em;font-size:150%;text-align:left" @click="dispatch('AttemptLogin','login.salesforce.com')">
                    <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                    Production (login.salesforce.com)
                </button>
            </div>
        </li>
        <li style="margin:2em;">
            <div class="slds-box">
                <button class="slds-button slds-button_neutral" style="width:100%;padding:1em;font-size:150%;text-align:left" @click="dispatch('AttemptLogin','test.salesforce.com')">
                    <button-iconleft type="standard" icon="default" class="slds-icon_large"></button-iconleft>
                    Sandbox (test.salesforce.com)
                </button>
            </div>
        </li>
        <!--<li style="margin:2em;">
            <div class="slds-box">
                <button class="slds-button slds-button_neutral" style="width:100%;padding:1em;font-size:150%;text-align:left" @click="dispatch('AttemptLogin',customLoginUrl)" :disabled="!customLoginUrl">
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
	<dir-element v-model="orgSettingsPath" label=" settings file path"></dir-element>
    <div class="slds-float_right">
        <button class="slds-button slds-button_success" @click="dispatch('SetOrgSettingsPath',orgSettingsPath)">Save path</button>
    </div>
</v-modal>