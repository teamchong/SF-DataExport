Vue.component('organization-modal', {
    template,
    data() { return { fetchOrgSettingsPath: '' }; },
    watch: {
        fetchOrgSettingsPath(search) {
            if (search && search !== this.search) {
                this.$store.state.dispatch$.next({
                    type: 'fetchDirPath', payload: {
                        search, value: this.$store.state.orgSettingsPath, field: 'orgSettingsPathItems'
                    }
                });
            }
        },
    },
    computed: {
        currentInstanceUrl() { return this.$store.state.currentInstanceUrl; },
        orgSettings() { return this.$store.state.orgSettings; },
        orgSettingsPath: {
            get() { return this.$store.state.orgSettingsPath; },
            set(value) { this.dispatch('orgSettingsPath', value); },
        },
        orgSettingsPathItems() { return this.$store.state.orgSettingsPathItems; },
    },
});