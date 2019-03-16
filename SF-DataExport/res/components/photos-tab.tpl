<div>
    <div class="slds-page-header">
        <div class="slds-page-header__row">
            <div class="slds-page-header__col-title">
                <div class="slds-media">
                    <div class="slds-media__figure">
                        <span class="slds-icon_container slds-icon-standard-carousel" title="User Photos">
                            <svg class="slds-icon slds-page-header__icon">
                                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#carousel" />
                            </svg>
                        </span>
                    </div>
                    <div class="slds-media__body">
                        <div class="slds-page-header__name">
                            <div class="slds-page-header__name-title">
                                <h1>
                                    <span class="slds-page-header__title slds-truncate" title="User Photos">User Photos</span>
                                </h1>
                            </div>
                        </div><!--<p class="slds-page-header__name-meta">-</p>-->
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div style="padding:1em;position:relative;">
        <div v-if="currentInstanceUrl && userCount" id="user-photos-div">
            <a v-for="userPhoto in userPhotos" href="javascript:void(0)" @click="dispatch('viewPage',userPhoto.url)">
                <img :src="userPhoto.photo" />
                <span>{{userPhoto.name}}<br />{{userPhoto.role}}<br />{{userPhoto.profile}}</span>
            </a>
        </div>
        <spinner class="slds-spinner_medium" style="margin-top:2em;" v-if="currentInstanceUrl && !userCount"></spinner>
        <div v-if="!currentInstanceUrl" style="padding:5em;">
            <a href="javascript:void(0)" @click="dispatch('showOrgModal',true)">click here to login your organization.</a>
        </div>
    </div>
</div>