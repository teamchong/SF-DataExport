Vue.component('photos-tab', {
    template,
    computed: {
        currentInstanceUrl() {
            return this.$store.state.currentInstanceUrl;
        },
        userCount() { return this.$store.state.users.length; },
        userPhotos() {
            const { currentInstanceUrl, tab, users } = this.$store.state;
            if (tab !== 'photos') {
                return [];
            }
            return users
                .filter(u => u.FullPhotoUrl && !/\.content\.force\.com\/profilephoto\/005\//i.test(u.FullPhotoUrl))
                .map(u => ({
                    url: currentInstanceUrl + '/' + u.Id,
                    name: u.Name,
                    role: (u.UserRole || {}).Name || '',
                    profile: (u.Profile || {}).Name || '',
                    photo: u.FullPhotoUrl,
                }));
        },
    },
});