<v-modal @close="dispatch('showPhotosModal',false)" :modalStyle="{'min-width':'95vw',width:'95vw','max-width':'95vw'}">
    <template #header><b>User Profile Pictures</b></template>
    <div style="padding:1em;position:relative;">
        <div v-if="currentInstanceUrl && userCount" id="user-photos-div">
            <a v-for="userPhoto in userPhotos" href="javascript:void(0)" @click="dispatch('popoverUserId',userPhoto.id)">
                <img :src="userPhoto.photo" />
                <span>{{userPhoto.name}}<br />{{userPhoto.role}}<br />{{userPhoto.profile}}</span>
            </a>
        </div>
		<div style="height:6em" v-if="currentInstanceUrl && !userCount">
			<spinner class="slds-spinner slds-spinner_medium"></spinner>
		</div>
        <div v-if="!currentInstanceUrl" style="padding:5em;">
            <a href="javascript:void(0)" @click="dispatch('showOrgModal',true)">click here to login your organization.</a>
        </div>
    </div>
	<template #footer><i v-if="false"></i></template>
</v-modal>