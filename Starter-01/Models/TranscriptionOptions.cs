using System;
using System.Collections.Generic;
using Deepgram.Transcription;

namespace YourProject.Models
{
  public class TranscriptionOptions
  {
    public static void SetFeatures(PrerecordedTranscriptionOptions transcriptionOptions, string key, string value)
    {
      switch (key)
      {
        case "smart_format":
            transcriptionOptions.SmartFormat = bool.Parse(value);
            break;
        case "punctuate":
            transcriptionOptions.Punctuate = bool.Parse(value);
            break;
        case "paragraphs":
            transcriptionOptions.Paragraphs = bool.Parse(value);
            break;
        case "utterances":
            transcriptionOptions.Utterances = bool.Parse(value);
            break;
        case "numerals":
            transcriptionOptions.Numerals = bool.Parse(value);
            break;
        case "profanity_filter":
            transcriptionOptions.ProfanityFilter = bool.Parse(value);
            break;
        case "diarize":
            transcriptionOptions.Diarize = bool.Parse(value);
            break;
        case "summarize":
            transcriptionOptions.Summarize = "v2";
            break;
        case "detect_topics":
            transcriptionOptions.DetectTopics = bool.Parse(value);
            break;
        default:
            Console.WriteLine($"Feature {key} not recognized.");
            break;
      }
    }
  }
}
