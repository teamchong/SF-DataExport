<div class="slds-form-element" v-if="hasOfflineAccess">
  <label class="slds-form-element__label" :for="id">{{label}}</label>
    <div class="slds-form-element__control slds-input-has-icon slds-input-has-icon_left-right" style="background:#000;color:#fff;">
        <svg class="slds-icon slds-input__icon slds-input__icon_left slds-icon-text-default">
            <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/action-sprite/svg/symbols.svg#apex" />
        </svg>
        <input :placeholder="label" class="slds-input" type="text" v-model="cmd" readonly :id="id" @focus="$event.target.select();document.execCommand('Copy')" />
        <button class="slds-button slds-button_icon slds-input__icon slds-input__icon_right" title="Copy" @click="$event.target.previousElementSibling.select('cmdExport-input').select();document.execCommand('Copy')">
            <svg class="slds-button__icon slds-icon-text-light">
                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/utility-sprite/svg/symbols.svg#copy_to_clipboard" />
            </svg>
        </button>
    </div>
</div>