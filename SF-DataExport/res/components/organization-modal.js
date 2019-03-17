Vue.component('organization-modal', {
    template,
    computed: {
        currentInstanceUrl() { return this.$store.state.currentInstanceUrl; },
        orgSettings() { return this.$store.state.orgSettings; },
        orgSettingsPath: {
            get() { return this.$store.state.orgSettingsPath; },
            set(value) { this.dispatch('orgSettingsPath', value); }
        }
    }
});