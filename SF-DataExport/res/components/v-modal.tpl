<div style="height: 640px;position:fixed;top:1em;">
    <section tabindex="-1" class="slds-modal slds-fade-in-open">
        <div class="slds-modal__container">
            <header class="slds-modal__header">
                <button class="slds-button slds-button_icon slds-modal__close slds-button_icon-inverse" title="Close" @click="$emit('close')">
                    <svg class="slds-button__icon slds-button__icon_large">
                        <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/utility-sprite/svg/symbols.svg#close" />
                    </svg>
                    <span class="slds-assistive-text">
                        <slot name="close-text">Close</slot>
                    </span>
                </button>
                <slot name="header"></slot>
            </header>
            <div class="slds-modal__content slds-p-around_medium" id="modal-content-id-1">
                <slot></slot>
            </div>
            <footer class="slds-modal__footer">
                <button class="slds-button slds-button_neutral" @click="$emit('close')">
                    <slot name="close-text">Close</slot>
                </button>
            </footer>
        </div>
    </section>
    <div class="slds-backdrop slds-backdrop_open"></div>
</div>