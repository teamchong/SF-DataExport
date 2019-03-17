Vue.component('setup-tab', {
    template,
    computed: {
        chromePath: {
            get() { return this.$store.state.chromePath; },
            set(value) { this.dispatch('chromePath', value); }
        },
        orgSettingsPath: {
            get() { return this.$store.state.orgSettingsPath; },
            set(value) { this.dispatch('orgSettingsPath', value); }
        }
    }
});