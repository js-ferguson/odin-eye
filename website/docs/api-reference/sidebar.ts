import type { SidebarsConfig } from "@docusaurus/plugin-content-docs";

const sidebar: SidebarsConfig = {
  apisidebar: [
    {
      type: "category",
      label: "Bosses",
      link: {
        type: "doc",
        id: "api-reference/boss",
      },
      items: [
        {
          type: "doc",
          id: "api-reference/retrieve-boss-progression",
          label: "Retrieve boss progression",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Character Stats",
      link: {
        type: "doc",
        id: "api-reference/character-stats",
      },
      items: [
        {
          type: "doc",
          id: "api-reference/retrieve-every-characters-submitted-stats",
          label: "Retrieve every character's submitted stats",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api-reference/submit-a-characters-stats",
          label: "Submit a character's stats",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api-reference/retrieve-what-each-characters-odin-eye-client-has-reported-about-itself",
          label: "Retrieve what each character's OdinEye.Client has reported about itself",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Players",
      link: {
        type: "doc",
        id: "api-reference/player",
      },
      items: [
        {
          type: "doc",
          id: "api-reference/retrieve-live-cheat-detection-status",
          label: "Retrieve live cheat-detection status",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api-reference/submit-a-characters-cheat-detection-status",
          label: "Submit a character's cheat-detection status",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api-reference/show-a-hud-banner-to-one-connected-player",
          label: "Show a HUD banner to one connected player",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api-reference/list-all-connected-players",
          label: "List all connected players",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Events",
      link: {
        type: "doc",
        id: "api-reference/events",
      },
      items: [
        {
          type: "doc",
          id: "api-reference/poll-the-durable-event-feed",
          label: "Poll the durable event feed",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api-reference/report-client-observed-events",
          label: "Report client-observed events",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "Server",
      link: {
        type: "doc",
        id: "api-reference/server",
      },
      items: [
        {
          type: "doc",
          id: "api-reference/retrieve-game-server-info",
          label: "Retrieve game server info",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "World",
      link: {
        type: "doc",
        id: "api-reference/world",
      },
      items: [
        {
          type: "doc",
          id: "api-reference/retrieve-game-world-info",
          label: "Retrieve game world info",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api-reference/retrieve-the-worlds-current-difficulty-modifiers",
          label: "Retrieve the world's current difficulty modifiers",
          className: "api-method get",
        },
      ],
    },
  ],
};

export default sidebar.apisidebar;
