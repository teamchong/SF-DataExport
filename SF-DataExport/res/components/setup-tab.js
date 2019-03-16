Vue.component('setup-tab', {
    template,
    data() { return { fetchOrgSettingsPath: '', fetchChromePath: '' }; },
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
        fetchChromePath(search) {
            if (search && search !== this.search) {
                this.$store.state.dispatch$.next({
                    type: 'fetchPath', payload: {
                        search, value: this.$store.state.chromePath, field: 'chromePathItems'
                    }
                });
            }
        },
    },
    computed: {
        chromePath: {
            get() { return this.$store.state.chromePath; },
            set(value) { this.dispatch('chromePath', value); },
        },
        chromePathItems() { return this.$store.state.chromePathItems; },
        orgSettingsPath: {
            get() { return this.$store.state.orgSettingsPath; },
            set(value) { this.dispatch('orgSettingsPath', value); },
        },
        orgSettingsPathItems() { return this.$store.state.orgSettingsPathItems; },
    },
});